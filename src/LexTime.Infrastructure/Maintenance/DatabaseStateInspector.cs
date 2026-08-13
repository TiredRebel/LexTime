using LexTime.Infrastructure.Persistence;
using LexTime.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Maintenance;

/// <summary>How much data the database currently holds, relative to what a full seed produces.</summary>
public enum SeedState
{
    /// <summary>No rows in any table. Safe to seed.</summary>
    Empty,

    /// <summary>Every table holds exactly the expected number of rows.</summary>
    Complete,

    /// <summary>Some rows, but not the expected counts. A seed was interrupted.</summary>
    Partial,
}

/// <summary>
/// Reports whether the database is empty, fully seeded, or somewhere in between.
/// </summary>
/// <remarks>
/// The distinction that matters is <see cref="SeedState.Partial"/>. A seed interrupted
/// midway leaves a database that looks populated and is not, and a check asking only "are
/// there any rows" reports it complete. Feature 003's rollup would then report on it
/// faithfully and produce totals that are wrong. Counting per table against the expected
/// volumes is what makes that state loud instead of silent — see
/// specs/002-bootstrap-and-seed/research.md R6.
/// </remarks>
/// <param name="context">The context to count through.</param>
public sealed class DatabaseStateInspector(LexTimeDbContext context)
{
    /// <summary>
    /// Counts each table and classifies the result.
    /// </summary>
    /// <param name="expected">
    /// The volumes a complete seed would have produced. Passed in rather than read from
    /// configuration, so a test that seeded at reduced scale is judged against the scale it
    /// actually used.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The state, and the counts it was derived from.</returns>
    public async Task<DatabaseStateReport> InspectAsync(
        SeedOptions expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var users = await context.Users.CountAsync(cancellationToken).ConfigureAwait(false);
        var clients = await context.Clients.CountAsync(cancellationToken).ConfigureAwait(false);
        var matters = await context.Matters.CountAsync(cancellationToken).ConfigureAwait(false);
        var entries = await context.TimeEntries.CountAsync(cancellationToken).ConfigureAwait(false);

        var state = (users, clients, matters, entries) switch
        {
            (0, 0, 0, 0) => SeedState.Empty,
            _ when users == expected.UserCount
                && clients == expected.ClientCount
                && matters == expected.MatterCount
                && entries == expected.TimeEntryCount => SeedState.Complete,
            _ => SeedState.Partial,
        };

        return new DatabaseStateReport(state, users, clients, matters, entries);
    }
}

/// <summary>The classification and the counts behind it.</summary>
/// <param name="State">Empty, Complete or Partial.</param>
/// <param name="Users">Rows in <c>dbo.Users</c>.</param>
/// <param name="Clients">Rows in <c>dbo.Clients</c>.</param>
/// <param name="Matters">Rows in <c>dbo.Matters</c>.</param>
/// <param name="TimeEntries">Rows in <c>dbo.TimeEntries</c>.</param>
public sealed record DatabaseStateReport(
    SeedState State,
    int Users,
    int Clients,
    int Matters,
    int TimeEntries);
