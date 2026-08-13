using LexTime.Infrastructure.Persistence;

namespace LexTime.IntegrationTests;

/// <summary>
/// Asserts that <c>WorkDate</c> carries no database constraint (FR-012).
/// </summary>
/// <remarks>
/// This class exists to defend an absence, which is the kind of requirement most easily
/// broken by someone trying to help. A missing date constraint reads as an oversight to
/// anyone opening the model later; adding one would reject the 24 months of history feature
/// 002 seeds, and would make the database progressively reject its own contents as time
/// passed. Without this test that breakage is silent until the seed fails.
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class WorkDateConstraintTests(SqlServerFixture fixture)
{
    private const string ClientCode = "DATE";
    private const string UserEmail = "workdate@lextime.test";

    /// <summary>
    /// An entry dated three years in the past is accepted. The 90-day backdating limit is a
    /// submission rule enforced in application code, not a storage invariant.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task AcceptsWorkDateFarOutsideTheBackdatingWindow()
    {
        await using var context = fixture.CreateContext();
        var (userId, matterId) = await ArrangeAsync(context, "old").ConfigureAwait(true);

        await DirectSql.InsertTimeEntryAsync(
            context, userId, matterId, 60, new DateOnly(2023, 8, 13)).ConfigureAwait(true);
    }

    /// <summary>
    /// A date at the far edge of the range feature 002 seeds is accepted, so the seed cannot
    /// be rejected at its oldest boundary.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task AcceptsWorkDateAtTheOldestSeededBoundary()
    {
        await using var context = fixture.CreateContext();
        var (userId, matterId) = await ArrangeAsync(context, "boundary").ConfigureAwait(true);

        await DirectSql.InsertTimeEntryAsync(
            context, userId, matterId, 6, new DateOnly(2024, 8, 13)).ConfigureAwait(true);
    }

    /// <summary>
    /// Ensures a timekeeper, client and matter exist to hang entries from.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="discriminator">Makes the matter number unique per case.</param>
    /// <returns>The user and matter identifiers to record against.</returns>
    private static async Task<(int UserId, int MatterId)> ArrangeAsync(
        LexTimeDbContext context,
        string discriminator)
    {
        var user = context.Users.FirstOrDefault(u => u.Email == UserEmail);
        var userId = user?.UserId
            ?? await DirectSql.InsertUserAsync(context, UserEmail).ConfigureAwait(false);

        var client = context.Clients.FirstOrDefault(c => c.ClientCode == ClientCode);
        var clientId = client?.ClientId
            ?? await DirectSql.InsertClientAsync(context, ClientCode).ConfigureAwait(false);

        var matterId = await DirectSql
            .InsertMatterAsync(context, clientId, $"{ClientCode}-{discriminator}")
            .ConfigureAwait(false);

        return (userId, matterId);
    }
}
