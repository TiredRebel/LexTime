using Microsoft.Data.SqlClient;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the duration check constraint (FR-011, SC-005), asserted against the database
/// directly so that application-layer validation cannot mask a missing constraint.
/// </summary>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class DurationConstraintTests(SqlServerFixture fixture)
{
    private const string ClientCode = "DUR";
    private const string UserEmail = "duration@lextime.test";

    /// <summary>
    /// Durations that violate the six-minute increment rule, the positivity rule, or the
    /// 1440-minute ceiling are rejected by the database.
    /// </summary>
    /// <param name="durationMinutes">The invalid duration to attempt.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Theory]
    [InlineData(7)]      // not a multiple of six
    [InlineData(0)]      // not positive
    [InlineData(-6)]     // negative, though a multiple of six
    [InlineData(1446)]   // a multiple of six, but beyond a day
    public async Task RejectsInvalidDuration(int durationMinutes)
    {
        await using var context = fixture.CreateContext();
        var (userId, matterId) = await ArrangeAsync(context, durationMinutes).ConfigureAwait(true);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            DirectSql.InsertTimeEntryAsync(
                context, userId, matterId, durationMinutes, new DateOnly(2026, 3, 2)))
            .ConfigureAwait(true);

        Assert.Contains("CK_TimeEntries_DurationMinutes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A duration of six minutes — one tenth of a billable hour, the smallest legal
    /// increment — is accepted.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task AcceptsSmallestValidDuration()
    {
        await using var context = fixture.CreateContext();
        var (userId, matterId) = await ArrangeAsync(context, 6).ConfigureAwait(true);

        await DirectSql.InsertTimeEntryAsync(
            context, userId, matterId, 6, new DateOnly(2026, 3, 3)).ConfigureAwait(true);
    }

    /// <summary>
    /// A full day of 1440 minutes sits exactly on the ceiling and is accepted. The
    /// boundary matters: an off-by-one in the constraint would reject it.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task AcceptsDurationExactlyAtTheCeiling()
    {
        await using var context = fixture.CreateContext();
        var (userId, matterId) = await ArrangeAsync(context, 1440).ConfigureAwait(true);

        await DirectSql.InsertTimeEntryAsync(
            context, userId, matterId, 1440, new DateOnly(2026, 3, 4)).ConfigureAwait(true);
    }

    /// <summary>
    /// Ensures a timekeeper, client and matter exist to hang entries from, reusing them
    /// across cases in this class.
    /// </summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="discriminator">Makes the matter number unique per case.</param>
    /// <returns>The user and matter identifiers to record against.</returns>
    private static async Task<(int UserId, int MatterId)> ArrangeAsync(
        Infrastructure.Persistence.LexTimeDbContext context,
        int discriminator)
    {
        var userId = await EnsureUserAsync(context).ConfigureAwait(false);
        var clientId = await EnsureClientAsync(context).ConfigureAwait(false);
        var matterId = await DirectSql
            .InsertMatterAsync(context, clientId, $"{ClientCode}-{discriminator}")
            .ConfigureAwait(false);

        return (userId, matterId);
    }

    /// <summary>Inserts the shared timekeeper if this class has not already created it.</summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <returns>The timekeeper's identifier.</returns>
    private static async Task<int> EnsureUserAsync(Infrastructure.Persistence.LexTimeDbContext context)
    {
        var existing = context.Users.FirstOrDefault(u => u.Email == UserEmail);
        return existing?.UserId ?? await DirectSql.InsertUserAsync(context, UserEmail).ConfigureAwait(false);
    }

    /// <summary>Inserts the shared client if this class has not already created it.</summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <returns>The client's identifier.</returns>
    private static async Task<int> EnsureClientAsync(Infrastructure.Persistence.LexTimeDbContext context)
    {
        var existing = context.Clients.FirstOrDefault(c => c.ClientCode == ClientCode);
        return existing?.ClientId ?? await DirectSql.InsertClientAsync(context, ClientCode).ConfigureAwait(false);
    }
}
