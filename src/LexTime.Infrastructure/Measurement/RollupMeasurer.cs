using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LexTime.Infrastructure.Reporting;
using Microsoft.Data.SqlClient;

namespace LexTime.Infrastructure.Measurement;

/// <summary>
/// Takes one reading of the rollup: read counts, elapsed time, row count, result hash, and —
/// separately — the actual execution plan.
/// </summary>
/// <remarks>
/// Everything here reports what happened. Nothing decides what a good result would be, and
/// nothing fails because a number is disappointing: constitution P8 makes the measurement the
/// claim, so a measurer that treated a modest improvement as an error would be an instruction
/// to keep running until it said something better.
/// </remarks>
/// <param name="connectionString">
/// The database to measure. Supplied by the registration in <c>DependencyInjection</c>, which
/// has already resolved and validated it.
/// </param>
public sealed partial class RollupMeasurer(string connectionString)
{
    /// <summary>First day of the seeded window — 24 months back from the seed's reference date.</summary>
    public static readonly DateOnly RangeStart = new(2024, 8, 13);

    /// <summary>Last day of the seeded window, and the seed's committed reference date.</summary>
    public static readonly DateOnly RangeEnd = new(2026, 8, 13);

    /// <summary>
    /// Empties the buffer pool so a reading starts from disk.
    /// </summary>
    /// <remarks>
    /// <b>Instance-wide, not database-wide.</b> Harmless against the single-purpose container
    /// the quickstart brings up, and unwelcome anywhere else. The verb says so out loud before
    /// it starts rather than leaving it to a footnote.
    /// <para>
    /// Applied identically to both index states. Which convention is chosen matters far less
    /// than that it is held constant — measuring one state warm and the other cold would
    /// compare the cache, and whichever ran second would win.
    /// </para>
    /// </remarks>
    private const string ClearCacheStatements = "CHECKPOINT; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;";

    /// <summary>Turns the statistics on for a connection.</summary>
    private const string StatisticsOn = "SET STATISTICS IO ON; SET STATISTICS TIME ON;";

    /// <summary>Turns them off again, so the plan capture is not buried in message traffic.</summary>
    private const string StatisticsOff = "SET STATISTICS IO OFF; SET STATISTICS TIME OFF;";

    /// <summary>Matches the logical-read count in a <c>STATISTICS IO</c> line.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"logical reads (\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LogicalReadsPattern();

    /// <summary>Matches the elapsed time in a <c>STATISTICS TIME</c> line.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"elapsed time = (\d+) ms", RegexOptions.CultureInvariant)]
    private static partial Regex ElapsedPattern();

    /// <summary>
    /// Takes one reading of one combination, from a cold buffer pool.
    /// </summary>
    /// <remarks>
    /// Does not change the index state — the caller arranges that and says which state this
    /// reading is being taken in. Passing a state that does not match the database is how
    /// mislabelled figures happen, which is why the verb ensures the index on entry rather than
    /// trusting that the last run cleaned up after itself.
    /// </remarks>
    /// <param name="state">The index state the database is currently in.</param>
    /// <param name="shape">Which call to measure.</param>
    /// <param name="clientId">The client to filter to when <paramref name="shape"/> is single-client.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The captured reading.</returns>
    public async Task<MeasurementReading> TakeReadingAsync(
        IndexState state,
        RequestShape shape,
        int clientId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);

        var messages = new List<string>();
        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (var clear = new SqlCommand(ClearCacheStatements, connection))
        {
            await ExecuteAsync(clear, cancellationToken).ConfigureAwait(false);
        }

        // Cleared before the handler starts collecting, so no DBCC chatter reaches the capture.
        messages.Clear();
        await using (var on = new SqlCommand(StatisticsOn, connection))
        {
            await ExecuteAsync(on, cancellationToken).ConfigureAwait(false);
        }

        var (rowCount, hash) = await ReadResultAsync(connection, shape, clientId, cancellationToken)
            .ConfigureAwait(false);

        await using (var off = new SqlCommand(StatisticsOff, connection))
        {
            await ExecuteAsync(off, cancellationToken).ConfigureAwait(false);
        }

        var raw = string.Join(Environment.NewLine, messages);

        return new MeasurementReading(
            state,
            shape,
            SumLogicalReads(raw),
            MaxElapsed(raw),
            rowCount,
            hash,
            raw);
    }

    /// <summary>
    /// Captures the actual execution plan for a combination.
    /// </summary>
    /// <remarks>
    /// A separate execution rather than a pass combined with the statistics capture. Combining
    /// them interleaves plan and data result sets for no benefit, and the run is deterministic
    /// enough that a second execution describes the same work.
    /// <para>
    /// The plan carries runtime counters, so it is the <em>actual</em> plan rather than an
    /// estimate. That matters: the interesting part of this comparison is what happened,
    /// including whether a sort spilled.
    /// </para>
    /// </remarks>
    /// <param name="shape">Which call to capture.</param>
    /// <param name="clientId">The client to filter to when the shape is single-client.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The plan as XML, or an empty string if the server returned none.</returns>
    public async Task<string> CapturePlanAsync(
        RequestShape shape,
        int clientId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var planOn = new SqlCommand("SET STATISTICS XML ON;", connection))
        {
            await ExecuteAsync(planOn, cancellationToken).ConfigureAwait(false);
        }

        string? plan = null;

        await using (var command = CreateRollupCommand(connection, shape, clientId))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            do
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    // The plan arrives as its own single-column result set, interleaved with
                    // the data. Identified by content rather than by position, because the
                    // number of result sets depends on how many statements ran.
                    if (reader.FieldCount == 1
                        && reader.GetValue(0) is string candidate
                        && candidate.Contains("ShowPlanXML", StringComparison.Ordinal))
                    {
                        plan = candidate;
                    }
                }
            }
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
        }

        await using (var planOff = new SqlCommand("SET STATISTICS XML OFF;", connection))
        {
            await ExecuteAsync(planOff, cancellationToken).ConfigureAwait(false);
        }

        return plan ?? string.Empty;
    }

    /// <summary>Builds the rollup call for a shape.</summary>
    /// <param name="connection">The connection to bind to.</param>
    /// <param name="shape">Which call.</param>
    /// <param name="clientId">Client to filter to when the shape is single-client.</param>
    /// <returns>A command the caller owns and must dispose.</returns>
    private static SqlCommand CreateRollupCommand(
        SqlConnection connection,
        RequestShape shape,
        int clientId)
    {
        var command = new SqlCommand(SqlWeeklyBillableRollupReader.ProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 600,
        };

        command.Parameters.Add("@FromDate", SqlDbType.Date).Value =
            RangeStart.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@ToDate", SqlDbType.Date).Value =
            RangeEnd.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@ClientId", SqlDbType.Int).Value =
            shape == RequestShape.SingleClient ? clientId : DBNull.Value;

        return command;
    }

    /// <summary>
    /// Runs the rollup, counting rows and hashing the ordered result set.
    /// </summary>
    /// <remarks>
    /// The hash is what proves equivalence at full scale. A row-by-row test cannot load 400,000
    /// entries, and the measurement is already reading every row of both states — so comparing
    /// two hashes costs almost nothing and covers the claim the tests cannot reach.
    /// <para>
    /// Every value is formatted invariantly and separated by characters that cannot occur in
    /// the data, so two runs of identical data hash identically and two different result sets
    /// cannot collide by rearranging field boundaries.
    /// </para>
    /// </remarks>
    /// <param name="connection">An open connection.</param>
    /// <param name="shape">Which call.</param>
    /// <param name="clientId">Client to filter to when the shape is single-client.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row count and the hash.</returns>
    private static async Task<(int RowCount, string Hash)> ReadResultAsync(
        SqlConnection connection,
        RequestShape shape,
        int clientId,
        CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rows = 0;
        var buffer = new StringBuilder();

        await using var command = CreateRollupCommand(connection, shape, clientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            buffer.Clear();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                buffer.Append(
                    reader.IsDBNull(i)
                        ? " "
                        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture));
                buffer.Append('\u001F'); // unit separator: cannot occur in the data
            }

            buffer.Append('\u001E'); // record separator
            hasher.AppendData(Encoding.UTF8.GetBytes(buffer.ToString()));
            rows++;
        }

        return (rows, Convert.ToHexString(hasher.GetHashAndReset()));
    }

    /// <summary>Runs a statement that returns nothing.</summary>
    /// <remarks>
    /// Takes a constructed command rather than a string. Passing the text through a helper hides
    /// the literal from CA2100, which then fires on the helper — and the correct response to
    /// that is to let the analyzer see the constant, not to suppress it. This feature adds no
    /// CA2100 suppression, and its research recorded in advance that one appearing here would be
    /// a design error rather than a finding to justify.
    /// </remarks>
    /// <param name="command">The command to run. Constructed from a constant at every call site.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the statement has run.</returns>
    private static async Task ExecuteAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Totals the logical reads the statistics output reported, across every table it named.
    /// </summary>
    /// <remarks>
    /// Summed rather than reported per table because the comparison is between two plans for
    /// the same query, and a plan that moves work from one table to another has not saved
    /// anything. The per-table breakdown survives verbatim in the committed raw output for
    /// anyone who wants it.
    /// </remarks>
    /// <param name="raw">The verbatim statistics text.</param>
    /// <returns>Total logical reads.</returns>
    private static long SumLogicalReads(string raw) =>
        LogicalReadsPattern().Matches(raw)
            .Sum(m => long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));

    /// <summary>
    /// The largest elapsed time the statistics output reported.
    /// </summary>
    /// <remarks>
    /// <c>SET STATISTICS TIME</c> emits a line per statement plus one for the batch, and the
    /// batch total is the largest of them. Taking the maximum rather than the last is the same
    /// answer and does not depend on the order the server chose to report in.
    /// </remarks>
    /// <param name="raw">The verbatim statistics text.</param>
    /// <returns>Elapsed milliseconds.</returns>
    private static long MaxElapsed(string raw)
    {
        var matches = ElapsedPattern().Matches(raw);

        return matches.Count == 0
            ? 0
            : matches.Max(m => long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
    }
}
