using Microsoft.Data.SqlClient;

namespace LexTime.Infrastructure.Measurement;

/// <summary>
/// The covering index the rollup is measured with and without.
/// </summary>
/// <remarks>
/// The index itself is created by a migration and is part of the schema every clone receives.
/// This type exists only so the measurement can take it away for the length of a reading and
/// put it back — the "before" state is something the measurement manufactures deliberately and
/// briefly, not a state the repository ships in.
/// <para>
/// <b>The statement below must stay identical to the migration's.</b> If the two drift, the
/// measurement's "with index" readings are taken against a different index from the one that
/// ships, and every published figure quietly stops describing the repository.
/// <c>CoveringIndexTests</c> asserts the definition after a drop-and-recreate cycle for exactly
/// that reason.
/// </para>
/// </remarks>
public static class CoveringIndex
{
    /// <summary>The index name, as the migration creates it and <c>sys.indexes</c> records it.</summary>
    public const string Name = "IX_TimeEntries_WorkDate_Billable";

    /// <summary>The table it sits on.</summary>
    public const string TableName = "dbo.TimeEntries";

    /// <summary>
    /// Recreates the index exactly as the migration does.
    /// </summary>
    /// <remarks>
    /// A constant, not a composed string. This feature adds no <c>CA2100</c> suppression, and
    /// under the plan's research a suppression appearing here would be a design error rather
    /// than a finding to justify.
    /// </remarks>
    private const string CreateStatement = """
        CREATE NONCLUSTERED INDEX IX_TimeEntries_WorkDate_Billable
            ON dbo.TimeEntries (WorkDate, IsBillable)
            INCLUDE (MatterId, DurationMinutes, HourlyRateSnapshot);
        """;

    /// <summary>Removes the index. Used only by the measurement, and always paired with a restore.</summary>
    private const string DropStatement =
        "DROP INDEX IX_TimeEntries_WorkDate_Billable ON dbo.TimeEntries;";

    /// <summary>Asks whether the index currently exists on the table.</summary>
    private const string ExistsQuery = """
        SELECT COUNT(*)
        FROM sys.indexes
        WHERE name = 'IX_TimeEntries_WorkDate_Billable'
          AND object_id = OBJECT_ID('dbo.TimeEntries');
        """;

    /// <summary>
    /// Whether the index is present.
    /// </summary>
    /// <remarks>
    /// Worth asking even when a migration is recorded as applied. EF compares migration
    /// history, not schema: an index dropped by hand is never restored by re-running
    /// <c>migrate</c>, and the database goes on reporting itself fully migrated.
    /// </remarks>
    /// <param name="connection">An open connection to the database to inspect.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns><see langword="true"/> when the index exists.</returns>
    public static async Task<bool> ExistsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var command = new SqlCommand(ExistsQuery, connection);
        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>
    /// Creates the index if it is missing, and does nothing if it is already there.
    /// </summary>
    /// <remarks>
    /// Called before a measurement starts as well as after it finishes. The entry call is the
    /// one that is easy to leave out and expensive to omit: a run interrupted midway leaves the
    /// index dropped, and the next run would then take its "with index" readings without one
    /// and label them as if it had. Two mislabelled figures out of four, with nothing failing.
    /// </remarks>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> if the index had to be created.</returns>
    public static async Task<bool> EnsureAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using var command = new SqlCommand(CreateStatement, connection)
        {
            CommandTimeout = 300,
        };
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Drops the index if it is present.
    /// </summary>
    /// <remarks>
    /// Only ever called by the measurement, and only inside a scope that restores it. Any other
    /// caller is a bug: the repository's committed state has this index.
    /// </remarks>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> if the index had to be dropped.</returns>
    public static async Task<bool> DropAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using var command = new SqlCommand(DropStatement, connection)
        {
            CommandTimeout = 300,
        };
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
