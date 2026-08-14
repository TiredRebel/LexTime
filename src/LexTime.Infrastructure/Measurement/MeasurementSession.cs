using System.Globalization;
using Microsoft.Data.SqlClient;

namespace LexTime.Infrastructure.Measurement;

/// <summary>
/// Runs a whole measurement: every combination, several readings each, the plans, the files,
/// and the equivalence check.
/// </summary>
/// <remarks>
/// The index is restored on every exit path. That is not tidiness — a run that fails and also
/// leaves the schema degraded produces a database that reports itself fully migrated and is
/// missing an index, and the only symptom is that the report is slower than the repository
/// claims. See <see cref="CoveringIndex.EnsureAsync"/>.
/// </remarks>
/// <param name="measurer">Takes the individual readings.</param>
/// <param name="connectionString">Used for the index toggle and the busiest-client lookup.</param>
public sealed class MeasurementSession(RollupMeasurer measurer, string connectionString)
{
    /// <summary>
    /// Finds the client with the most logged minutes, for the single-client shape.
    /// </summary>
    /// <remarks>
    /// The busiest rather than an arbitrary one, so the shape is reproducible from the
    /// committed seed and its row count is nowhere near zero. Derived rather than hard-coded,
    /// so it stays correct if the seed's shape is ever regenerated.
    /// <para>
    /// <c>ClientId</c> breaks ties. Without it <c>TOP (1)</c> over equal totals returns whichever
    /// row the plan happened to produce first, and the measurement would silently compare two
    /// runs of different clients — in a feature whose entire premise is that two runs are
    /// comparable. The seed's activity is heavily skewed so a tie at the top is most unlikely;
    /// the point is that "unlikely" is not the standard this repository is measuring against.
    /// </para>
    /// </remarks>
    private const string BusiestClientQuery = """
        SELECT TOP (1) m.ClientId
        FROM dbo.TimeEntries AS te
        INNER JOIN dbo.Matters AS m ON m.MatterId = te.MatterId
        GROUP BY m.ClientId
        ORDER BY SUM(CAST(te.DurationMinutes AS bigint)) DESC, m.ClientId;
        """;

    /// <summary>
    /// Measures every combination and writes the evidence.
    /// </summary>
    /// <param name="readings">Readings per combination.</param>
    /// <param name="outputDirectory">Where plans and raw statistics are written.</param>
    /// <param name="includeSingleClient">
    /// Whether to measure the single-client shape as well as the full range.
    /// </param>
    /// <param name="progress">Receives a line per step, so a long run is not silent.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The reduced combinations, in the order they were measured.</returns>
    public async Task<IReadOnlyList<MeasuredCombination>> RunAsync(
        int readings,
        string outputDirectory,
        bool includeSingleClient,
        Action<string> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(readings, 1);
        ArgumentNullException.ThrowIfNull(progress);

        Directory.CreateDirectory(outputDirectory);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Before anything else. A previous run killed midway leaves the index dropped, and
        // without this the "with index" readings below would be taken without one and labelled
        // as though they had it — two wrong figures out of four, and nothing failing.
        if (await CoveringIndex.EnsureAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            progress($"{CoveringIndex.Name} was missing and has been restored before measuring.");
        }

        var clientId = await FindBusiestClientAsync(connection, cancellationToken).ConfigureAwait(false);
        progress($"single-client shape will use ClientId {clientId} (the busiest).");

        var shapes = includeSingleClient
            ? new[] { RequestShape.FullRange, RequestShape.SingleClient }
            : [RequestShape.FullRange];

        var results = new List<MeasuredCombination>();

        try
        {
            // Without the index first, so the run ends in the committed state even on the
            // ordinary path rather than relying solely on the restore below.
            foreach (var state in new[] { IndexState.WithoutIndex, IndexState.WithIndex })
            {
                await SetIndexStateAsync(connection, state, cancellationToken).ConfigureAwait(false);

                foreach (var shape in shapes)
                {
                    progress($"measuring {shape} {state} ({readings} readings)...");

                    results.Add(await MeasureCombinationAsync(
                        state, shape, clientId, readings, outputDirectory, cancellationToken)
                        .ConfigureAwait(false));
                }
            }
        }
        finally
        {
            // Deliberately not observing the cancellation token: a cancelled run must still
            // leave the schema in its committed state.
            await CoveringIndex.EnsureAsync(connection, CancellationToken.None).ConfigureAwait(false);
        }

        await WriteSummaryAsync(results, outputDirectory, cancellationToken).ConfigureAwait(false);

        return results;
    }

    /// <summary>
    /// Writes the reduced figures to a committed file.
    /// </summary>
    /// <remarks>
    /// Without this the medians and ranges exist only on the terminal, and the published
    /// document's rule that every figure traces to a committed file quietly fails for half of
    /// them. Worse, a later run overwrites the per-reading captures while the document goes on
    /// quoting the earlier ones — the numbers stop matching their own evidence and nothing
    /// says so. Committing the reduction alongside the raw output is what keeps the two in
    /// step.
    /// </remarks>
    /// <param name="results">The measured combinations.</param>
    /// <param name="outputDirectory">Where to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the summary is on disk.</returns>
    private static async Task WriteSummaryAsync(
        IReadOnlyList<MeasuredCombination> results,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            "Reduced figures for the run whose raw captures sit beside this file.",
            "Logical reads are one figure because every reading agreed; elapsed times are a",
            "median with the full range across readings.",
            string.Empty,
            $"{"shape",-14}{"index",-14}{"logical reads",15}{"median ms",11}{"min ms",9}{"max ms",9}{"rows",8}  result hash",
        };

        lines.AddRange(results.Select(r =>
            $"{r.Shape,-14}{r.State,-14}{r.LogicalReads,15:N0}{r.ElapsedMedian,11:N0}" +
            $"{r.ElapsedMin,9:N0}{r.ElapsedMax,9:N0}{r.RowCount,8:N0}  {r.ResultHash[..16]}"));

        await File.WriteAllLinesAsync(
            Path.Combine(outputDirectory, "summary.txt"),
            lines,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the two index states agreed, for every shape measured.
    /// </summary>
    /// <remarks>
    /// This is the full-scale equivalence proof. A row-by-row test cannot afford to load
    /// 400,000 entries; the measurement reads every row of both states anyway, so comparing
    /// their hashes costs nothing and covers the claim the tests cannot reach.
    /// </remarks>
    /// <param name="results">The measured combinations.</param>
    /// <returns>The shapes whose two states disagreed. Empty means equivalent.</returns>
    public static IReadOnlyList<RequestShape> FindEquivalenceFailures(
        IReadOnlyList<MeasuredCombination> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return [.. results
            .GroupBy(r => r.Shape)
            .Where(g => g.Select(r => r.ResultHash).Distinct(StringComparer.Ordinal).Count() > 1
                || g.Select(r => r.RowCount).Distinct().Count() > 1)
            .Select(g => g.Key)];
    }

    /// <summary>Takes the readings for one combination and reduces them.</summary>
    /// <param name="state">Index state, already applied to the database.</param>
    /// <param name="shape">Which call.</param>
    /// <param name="clientId">Client for the single-client shape.</param>
    /// <param name="readings">How many readings to take.</param>
    /// <param name="outputDirectory">Where to write the plan and raw statistics.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The reduced combination.</returns>
    /// <exception cref="InvalidOperationException">
    /// Readings of the same combination disagreed on logical reads. That figure is a property
    /// of the plan and must not vary; averaging it away would hide whatever is varying.
    /// </exception>
    private async Task<MeasuredCombination> MeasureCombinationAsync(
        IndexState state,
        RequestShape shape,
        int clientId,
        int readings,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var taken = new List<MeasurementReading>(readings);

        for (var i = 0; i < readings; i++)
        {
            taken.Add(await measurer
                .TakeReadingAsync(state, shape, clientId, cancellationToken)
                .ConfigureAwait(false));
        }

        var distinctReads = taken.Select(r => r.LogicalReads).Distinct().ToList();
        if (distinctReads.Count > 1)
        {
            throw new InvalidOperationException(
                $"Logical reads varied across readings of {shape}/{state}: " +
                $"{string.Join(", ", distinctReads)}. That figure is deterministic, so this is " +
                "something changing underneath the measurement rather than noise to average.");
        }

        var elapsed = taken.Select(r => r.ElapsedMilliseconds).OrderBy(v => v).ToList();
        var plan = await measurer
            .CapturePlanAsync(shape, clientId, cancellationToken)
            .ConfigureAwait(false);

        var slug = $"{Slug(shape)}-{Slug(state)}";

        // Written exactly as the server sent it. The summary table is a transcription, and a
        // transcription is somewhere a number can quietly change; with the source committed
        // beside it, every published figure can be checked rather than trusted.
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, $"statistics-{slug}.txt"),
            taken[0].RawStatistics,
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, $"plan-{slug}.sqlplan"),
            plan,
            cancellationToken).ConfigureAwait(false);

        return new MeasuredCombination(
            state,
            shape,
            distinctReads[0],
            elapsed[elapsed.Count / 2],
            elapsed[0],
            elapsed[^1],
            taken[0].RowCount,
            taken[0].ResultHash,
            plan,
            taken[0].RawStatistics);
    }

    /// <summary>Drops or restores the index so the database matches the requested state.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="state">The state to put the database into.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once the state matches.</returns>
    private static async Task SetIndexStateAsync(
        SqlConnection connection,
        IndexState state,
        CancellationToken cancellationToken)
    {
        if (state == IndexState.WithoutIndex)
        {
            await CoveringIndex.DropAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CoveringIndex.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Finds the client with the most logged minutes.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The busiest client's identifier, or 0 when the database holds no entries.</returns>
    private static async Task<int> FindBusiestClientAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(BusiestClientQuery, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return value is null or DBNull
            ? 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Turns an enumeration value into a lower-case filename fragment.</summary>
    /// <param name="value">The value to render.</param>
    /// <returns>A filename-safe fragment.</returns>
    private static string Slug(object value) =>
        value.ToString()!.ToLowerInvariant();
}
