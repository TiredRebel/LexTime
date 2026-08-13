using System.Globalization;
using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Seeding;

/// <summary>One distribution check and the band it has to fall within.</summary>
/// <param name="Name">Short label, printed by the bootstrap script.</param>
/// <param name="Measured">The value observed in the seeded data.</param>
/// <param name="Band">Human-readable description of the acceptable range.</param>
/// <param name="Passed">Whether the measurement fell inside the band.</param>
public sealed record SeedCheck(string Name, double Measured, string Band, bool Passed);

/// <summary>
/// Measures the seeded dataset against the distribution bands in SC-004, SC-005 and SC-007.
/// </summary>
/// <remarks>
/// Runs at full volume against the real data, because that is the artefact whose properties
/// matter and the only place a developer needs the answer. It complements rather than
/// replaces the generator tests, which assert the same invariants at a hundredth of the
/// scale with no database and catch a regression before it is ever loaded.
/// </remarks>
/// <param name="context">Supplies the connection.</param>
public sealed class SeedVerifier(LexTimeDbContext context)
{
    /// <summary>
    /// Runs every check and reports each measurement alongside its band.
    /// </summary>
    /// <remarks>
    /// Measured values are reported whether or not they pass. A check that only says "ok"
    /// tells a reader nothing about how close to a boundary the data sits.
    /// </remarks>
    /// <param name="expected">The volumes the dataset was seeded with.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One result per check, in reporting order.</returns>
    public async Task<IReadOnlyList<SeedCheck>> VerifyAsync(
        SeedOptions expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var weekend = await ScalarAsync(
            """
            SELECT 100.0 * SUM(CASE WHEN DATEPART(weekday, WorkDate) IN (1, 7) THEN 1 ELSE 0 END)
                 / NULLIF(COUNT(*), 0)
            FROM dbo.TimeEntries;
            """, cancellationToken).ConfigureAwait(false);

        var nonBillable = await ScalarAsync(
            """
            SELECT 100.0 * SUM(CASE WHEN IsBillable = 0 THEN 1 ELSE 0 END)
                 / NULLIF(COUNT(*), 0)
            FROM dbo.TimeEntries;
            """, cancellationToken).ConfigureAwait(false);

        var topTenShare = await ScalarAsync(
            """
            WITH PerClient AS (
                SELECT m.ClientId, SUM(CAST(t.DurationMinutes AS BIGINT)) AS Minutes
                FROM dbo.TimeEntries t
                JOIN dbo.Matters m ON m.MatterId = t.MatterId
                GROUP BY m.ClientId)
            SELECT 100.0
                 * (SELECT SUM(Minutes) FROM (SELECT TOP (10) Minutes FROM PerClient ORDER BY Minutes DESC) AS Top10)
                 / NULLIF((SELECT SUM(Minutes) FROM PerClient), 0);
            """, cancellationToken).ConfigureAwait(false);

        var durationViolations = await ScalarAsync(
            """
            SELECT COUNT(*) FROM dbo.TimeEntries
            WHERE DurationMinutes <= 0 OR DurationMinutes % 6 <> 0 OR DurationMinutes > 1440;
            """, cancellationToken).ConfigureAwait(false);

        var afterReference = await ScalarAsync(
            $"SELECT COUNT(*) FROM dbo.TimeEntries WHERE WorkDate > '{expected.ReferenceDate:yyyy-MM-dd}';",
            cancellationToken).ConfigureAwait(false);

        var inactiveClients = await ScalarAsync(
            "SELECT 100.0 * SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) FROM dbo.Clients;",
            cancellationToken).ConfigureAwait(false);

        var inactiveWithHistory = await ScalarAsync(
            """
            SELECT COUNT(DISTINCT c.ClientId)
            FROM dbo.Clients c
            JOIN dbo.Matters m ON m.ClientId = c.ClientId
            JOIN dbo.TimeEntries t ON t.MatterId = m.MatterId
            WHERE c.IsActive = 0;
            """, cancellationToken).ConfigureAwait(false);

        return
        [
            new SeedCheck("weekend share", weekend, "< 10%", weekend < 10),
            new SeedCheck("non-billable share", nonBillable, "10-25%", nonBillable is >= 10 and <= 25),
            new SeedCheck("top-10 client share", topTenShare, ">= 50%", topTenShare >= 50),
            new SeedCheck("duration violations", durationViolations, "= 0", durationViolations == 0),
            new SeedCheck("entries after reference date", afterReference, "= 0", afterReference == 0),
            new SeedCheck("inactive clients", inactiveClients, "5-20%", inactiveClients is >= 5 and <= 20),
            new SeedCheck("inactive with history", inactiveWithHistory, ">= 1", inactiveWithHistory >= 1),
        ];
    }

    /// <summary>Executes an aggregate query returning one number.</summary>
    /// <param name="sql">The query. A compile-time literal at every call site.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The measured value, or zero when the table is empty.</returns>
    private async Task<double> ScalarAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

#pragma warning disable CA2100 // Every call site above passes a compile-time literal.
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
#pragma warning restore CA2100

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return value is null or DBNull
            ? 0
            : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
}
