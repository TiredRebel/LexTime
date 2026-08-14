using LexTime.Application.Reporting;
using LexTime.Infrastructure.Measurement;
using LexTime.Infrastructure.Reporting;
using Microsoft.Data.SqlClient;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the covering index: that it ships with the schema, and that it changes nothing about
/// what the report returns.
/// </summary>
/// <remarks>
/// The second of those is the one that matters. An index that changes results is the failure
/// that hides best — every figure stays plausible, stays internally consistent, and nobody
/// looks. It is the same shape of problem as a wrong window function, and it gets the same
/// answer: an assertion that does not depend on the thing being tested.
/// <para>
/// This class proves equivalence at a hundredth of production scale, which is what a test can
/// afford. The claim over all 400,000 entries is discharged by the <c>measure</c> verb, which
/// hashes both result sets while it is reading them anyway.
/// </para>
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class CoveringIndexTests(SqlServerFixture fixture)
{
    /// <summary>
    /// Reads an index's key and included columns from the catalogue.
    /// </summary>
    /// <remarks>
    /// Asserting the columns rather than the name. An index called the right thing over the
    /// wrong columns would satisfy a name check and silently measure something else.
    /// </remarks>
    private const string DefinitionQuery = """
        SELECT c.name, ic.is_included_column, ic.key_ordinal
        FROM sys.indexes AS i
        INNER JOIN sys.index_columns AS ic
            ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns AS c
            ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.name = 'IX_TimeEntries_WorkDate_Billable'
          AND i.object_id = OBJECT_ID('dbo.TimeEntries')
        ORDER BY ic.is_included_column, ic.key_ordinal, c.name;
        """;

    /// <summary>
    /// A freshly migrated database carries the index, over exactly the expected columns.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MigrationCreatesTheIndexOverTheExpectedColumns()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_IndexDefinition").ConfigureAwait(true);

        var (keys, included) = await ReadDefinitionAsync(connectionString).ConfigureAwait(true);

        // Key order is part of the definition: (WorkDate, IsBillable) supports a range scan on
        // the date. Reversed, it would not.
        Assert.Equal(["WorkDate", "IsBillable"], keys);

        Assert.Equal(
            ["DurationMinutes", "HourlyRateSnapshot", "MatterId"],
            included.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Dropping and recreating the index through the measurement's own helper reproduces the
    /// migration's definition exactly.
    /// </summary>
    /// <remarks>
    /// The measurement restores the index from a statement of its own rather than by re-running
    /// the migration. If that statement ever drifted from the migration's, every "with index"
    /// figure would describe an index the repository does not ship — and nothing else would
    /// notice, because both would exist and both would be called the same thing.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RecreatingTheIndexReproducesTheMigrationsDefinition()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_IndexRecreate").ConfigureAwait(true);

        var fromMigration = await ReadDefinitionAsync(connectionString).ConfigureAwait(true);

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync().ConfigureAwait(true);
            Assert.True(await CoveringIndex.DropAsync(connection).ConfigureAwait(true));
            Assert.False(await CoveringIndex.ExistsAsync(connection).ConfigureAwait(true));
            Assert.True(await CoveringIndex.EnsureAsync(connection).ConfigureAwait(true));
        }

        var fromHelper = await ReadDefinitionAsync(connectionString).ConfigureAwait(true);

        Assert.Equal(fromMigration.Keys, fromHelper.Keys);
        Assert.Equal(
            fromMigration.Included.OrderBy(c => c, StringComparer.Ordinal),
            fromHelper.Included.OrderBy(c => c, StringComparer.Ordinal));
    }

    /// <summary>
    /// The report returns identical rows with and without the index.
    /// </summary>
    /// <remarks>
    /// The whole reason this feature's first user story is about correctness rather than speed.
    /// Compares every field of every row, in order, because the ordering is part of the
    /// procedure's contract and an index that perturbed it would still be a change.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheIndexDoesNotChangeWhatTheReportReturns()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_IndexEquivalence").ConfigureAwait(true);

        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await RollupFixtureBuilder.PopulateAsync(context).ConfigureAwait(true);
        }

        var withIndex = await ReadRollupAsync(connectionString).ConfigureAwait(true);

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync().ConfigureAwait(true);
            await CoveringIndex.DropAsync(connection).ConfigureAwait(true);
        }

        var withoutIndex = await ReadRollupAsync(connectionString).ConfigureAwait(true);

        // Restored before any assertion can fail, so a red test cannot leave the database
        // degraded for whatever runs next.
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync().ConfigureAwait(true);
            await CoveringIndex.EnsureAsync(connection).ConfigureAwait(true);
        }

        Assert.NotEmpty(withIndex);
        Assert.Equal(withIndex.Count, withoutIndex.Count);

        for (var i = 0; i < withIndex.Count; i++)
        {
            Assert.Equal(withIndex[i], withoutIndex[i]);
        }
    }

    /// <summary>Runs the rollup over the fixture's range.</summary>
    /// <param name="connectionString">The database to report on.</param>
    /// <returns>The rows, in the procedure's own order.</returns>
    private static Task<IReadOnlyList<WeeklyBillableRollupRow>> ReadRollupAsync(string connectionString) =>
        new SqlWeeklyBillableRollupReader(connectionString).ReadAsync(
            new WeeklyBillableRollupQuery(
                RollupFixtureBuilder.RangeStart,
                RollupFixtureBuilder.RangeEnd,
                null));

    /// <summary>Reads the index's key and included column names from the catalogue.</summary>
    /// <param name="connectionString">The database to inspect.</param>
    /// <returns>Key columns in key order, and included columns.</returns>
    private static async Task<(List<string> Keys, List<string> Included)> ReadDefinitionAsync(
        string connectionString)
    {
        var keys = new List<string>();
        var included = new List<string>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new SqlCommand(DefinitionQuery, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            (reader.GetBoolean(1) ? included : keys).Add(reader.GetString(0));
        }

        return (keys, included);
    }
}
