using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.TimeEntries;
using LexTime.Infrastructure;
using LexTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.IntegrationTests;

/// <summary>
/// The write path over HTTP: that the rules are reached, that a refusal changes nothing, and
/// that the database's own guard survives.
/// </summary>
/// <remarks>
/// <see cref="TimeEntryRuleTests"/> asks whether each rule is <em>right</em>; this file asks
/// whether it is <em>reached</em>. Both are needed — a feature enforcing all six perfectly in a
/// class nothing called would pass that file completely and fail every test here.
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class TimeEntryWriteTests(SqlServerFixture fixture)
{
    /// <summary>The route under test.</summary>
    private const string Route = TimeEntryEndpoints.BaseRoute;

    /// <summary>A conforming submission is recorded, and comes back with a rate nobody sent.</summary>
    /// <remarks>
    /// The captured rate is the assertion that matters. It is not in the request, so its presence
    /// in the response is rule 6 working: the value was read from the timekeeper, not supplied.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RecordsAConformingEntryAndCapturesTheRate()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteRecord").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes: 90))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var entry = await response.Content.ReadFromJsonAsync<TimeEntryDto>().ConfigureAwait(true);

        Assert.NotNull(entry);
        Assert.True(entry.TimeEntryId > 0);
        Assert.Equal(90, entry.DurationMinutes);
        Assert.Equal(TestWorld.ActiveUserRate, entry.HourlyRateSnapshot);
        Assert.Null(entry.UpdatedAtUtc);
    }

    /// <summary>Each rule refuses through the endpoint, naming itself.</summary>
    /// <param name="minutes">Duration to submit.</param>
    /// <param name="daysBack">How far back to date it; negative means the future.</param>
    /// <param name="expectedRule">The rule name expected in the problem body.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(7, 1, "DurationIncrement")]
    [InlineData(1446, 1, "DurationMaximum")]
    [InlineData(60, -1, "BackdatingWindow")]
    [InlineData(60, 200, "BackdatingWindow")]
    public async Task RefusesAViolatingSubmissionAndNamesTheRule(
        int minutes,
        int daysBack,
        string expectedRule)
    {
        var (connectionString, world) = await this
            .BuildWorldAsync($"LexTime_WriteRefuse{expectedRule}{minutes}{daysBack}").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes, daysBack))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Contains(expectedRule, body, StringComparison.Ordinal);
    }

    /// <summary>An inactive matter is refused, and the body says it was the matter.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefusesAnInactiveMatterAndSaysWhichPartyFailed()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteInactiveMatter").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes: 60) with { MatterId = world.InactiveMatterId })
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Contains("ActiveMatterAndClient", body, StringComparison.Ordinal);
        Assert.Contains("matter is not active", body, StringComparison.Ordinal);
    }

    /// <summary>A submission wrong in two ways reports both.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReportsEveryBrokenRuleInOneResponse()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteMulti").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes: 7, daysBack: -5))
            .ConfigureAwait(true);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Contains("DurationIncrement", body, StringComparison.Ordinal);
        Assert.Contains("BackdatingWindow", body, StringComparison.Ordinal);
    }

    /// <summary>A timekeeper or matter that does not exist is a 404, not a rule violation.</summary>
    /// <remarks>
    /// Telling a caller their matter is "not active" when it was never there sends them to fix a
    /// matter they do not have.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReportsAMissingPartyAsNotFoundRatherThanARuleViolation()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteMissingParty").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes: 60) with { MatterId = 999_999 })
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// An entry outside the backdating window accepts a narrative change and refuses a date change.
    /// </summary>
    /// <remarks>
    /// The clarification made observable, and the whole reason the rules are field-scoped on
    /// update. Both halves are asserted in one test because either alone would be satisfied by a
    /// wrong implementation: skipping rule 4 entirely passes the first, and applying it always
    /// passes the second.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AllowsCorrectingAnOldEntryButNotReDatingIt()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteOldEntry").ConfigureAwait(true);

        // Written directly, because the API would refuse to record it — which is the situation
        // being tested: an entry that was legal when recorded and would not be now.
        long entryId;
        var oldDate = TestWorld.Today.AddDays(-200);
        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await DirectSql.InsertTimeEntryAsync(
                context, world.ActiveUserId, world.ActiveMatterId, 60, oldDate).ConfigureAwait(true);
            entryId = await context.TimeEntries.Select(e => e.TimeEntryId).MaxAsync().ConfigureAwait(true);
        }

        using var client = CreateClient(connectionString);

        using var narrativeOnly = await client
            .PutAsJsonAsync(
                $"{Route}/{entryId}",
                new ReviseTimeEntryCommand(world.ActiveMatterId, oldDate, 60, true, "Corrected wording."))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, narrativeOnly.StatusCode);

        using var reDated = await client
            .PutAsJsonAsync(
                $"{Route}/{entryId}",
                new ReviseTimeEntryCommand(world.ActiveMatterId, oldDate.AddDays(1), 60, true, "Corrected wording."))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, reDated.StatusCode);
        Assert.Contains(
            "BackdatingWindow",
            await reDated.Content.ReadAsStringAsync().ConfigureAwait(true),
            StringComparison.Ordinal);
    }

    /// <summary>A refused revision leaves the stored entry exactly as it was.</summary>
    /// <remarks>
    /// A partially applied update is worse than a refused one (FR-015). Compared field by field
    /// rather than by checking one value, because a handler that assigned some fields before
    /// evaluating would corrupt whichever it happened to reach first.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LeavesTheStoredEntryUntouchedWhenARevisionIsRefused()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteRefusedRevision").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var created = await client
            .PostAsJsonAsync(Route, NewEntry(world, minutes: 60))
            .ConfigureAwait(true);
        var before = await created.Content.ReadFromJsonAsync<TimeEntryDto>().ConfigureAwait(true);
        created.Dispose();

        using var refused = await client
            .PutAsJsonAsync(
                $"{Route}/{before!.TimeEntryId}",
                new ReviseTimeEntryCommand(world.ActiveMatterId, before.WorkDate, 7, false, "Should not be saved."))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var after = await client
            .GetFromJsonAsync<TimeEntryDto>($"{Route}/{before.TimeEntryId}")
            .ConfigureAwait(true);

        Assert.Equal(before, after);
    }

    /// <summary>
    /// A later rate change does not rewrite what an entry captured, even across a revision.
    /// </summary>
    /// <remarks>
    /// Rule 6's whole coverage — it has no refusing test, because no submission can violate it.
    /// This is the test that catches a revise handler which rebuilt the entity from the command
    /// and re-read the timekeeper's current rate: a mistake that would rewrite history on every
    /// edit, silently, and that nothing else here would notice.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NeverRewritesTheCapturedRate()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteRateSnapshot").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var created = await client.PostAsJsonAsync(Route, NewEntry(world, minutes: 60)).ConfigureAwait(true);
        var recorded = await created.Content.ReadFromJsonAsync<TimeEntryDto>().ConfigureAwait(true);
        created.Dispose();

        Assert.Equal(TestWorld.ActiveUserRate, recorded!.HourlyRateSnapshot);

        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.Users SET DefaultHourlyRate = 999.00 WHERE UserId = @id;",
                new SqlParameter("@id", world.ActiveUserId)).ConfigureAwait(true);
        }

        using var revised = await client
            .PutAsJsonAsync(
                $"{Route}/{recorded.TimeEntryId}",
                new ReviseTimeEntryCommand(world.ActiveMatterId, recorded.WorkDate, 60, true, "Reworded."))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);

        var after = await revised.Content.ReadFromJsonAsync<TimeEntryDto>().ConfigureAwait(true);

        Assert.Equal(TestWorld.ActiveUserRate, after!.HourlyRateSnapshot);
    }

    /// <summary>An entry can be deleted, and deleting it twice reports that it is gone.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DeletesAnEntryAndReportsASecondAttemptAsNotFound()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteDelete").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var created = await client.PostAsJsonAsync(Route, NewEntry(world, minutes: 60)).ConfigureAwait(true);
        var entry = await created.Content.ReadFromJsonAsync<TimeEntryDto>().ConfigureAwait(true);
        created.Dispose();

        using var first = await client.DeleteAsync(new Uri($"{Route}/{entry!.TimeEntryId}", UriKind.Relative))
            .ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var second = await client.DeleteAsync(new Uri($"{Route}/{entry.TimeEntryId}", UriKind.Relative))
            .ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>
    /// Two simultaneous submissions that individually fit cannot both be recorded.
    /// </summary>
    /// <remarks>
    /// Rule 3 is a read followed by a write, so without a transaction that refuses to let them
    /// interleave both requests read the same total, both pass, and the day ends above the
    /// maximum with neither request having been wrong. Nothing else in this suite would notice.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefusesToLetTwoConcurrentSubmissionsBreachTheDailyMaximum()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteConcurrent").ConfigureAwait(true);

        // 1,080 minutes already recorded. Two further submissions of 240 each fit individually
        // (1,320) and cannot both fit (1,560).
        var date = TestWorld.Today.AddDays(-3);
        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await DirectSql.InsertTimeEntryAsync(
                context, world.ActiveUserId, world.ActiveMatterId, 1080, date).ConfigureAwait(true);
        }

        using var first = CreateClient(connectionString);
        using var second = CreateClient(connectionString);

        var body = NewEntry(world, minutes: 240) with { WorkDate = date };

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(Route, body),
            second.PostAsJsonAsync(Route, body)).ConfigureAwait(true);

        try
        {
            var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);

            Assert.Equal(1, created);

            await using var context = SqlServerFixture.CreateContext(connectionString);
            var total = await context.TimeEntries
                .Where(e => e.UserId == world.ActiveUserId && e.WorkDate == date)
                .SumAsync(e => e.DurationMinutes).ConfigureAwait(true);

            Assert.True(total <= 1440, $"Day total reached {total}, above the 1440-minute maximum.");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// The database still refuses a violating duration written outside the application.
    /// </summary>
    /// <remarks>
    /// SC-010, and the reason constitution P6 calls the duplication deliberate. Feature 001 proved
    /// this constraint existed; this proves it still bites now that a second enforcement layer
    /// has arrived — which is exactly when someone concludes it is redundant and deletes it.
    /// </remarks>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task StorageStillRefusesAViolatingDurationWrittenOutsideTheApplication()
    {
        var (connectionString, world) = await this.BuildWorldAsync("LexTime_WriteConstraint").ConfigureAwait(true);

        await using var context = SqlServerFixture.CreateContext(connectionString);

        await Assert.ThrowsAsync<SqlException>(() => DirectSql.InsertTimeEntryAsync(
            context, world.ActiveUserId, world.ActiveMatterId, 7, TestWorld.Today.AddDays(-1)))
            .ConfigureAwait(true);
    }

    /// <summary>Builds a submission that breaks nothing, which callers perturb in one way.</summary>
    /// <param name="world">The fixture's identifiers.</param>
    /// <param name="minutes">Duration to submit.</param>
    /// <param name="daysBack">How far back to date it; negative dates it in the future.</param>
    /// <returns>The command.</returns>
    private static RecordTimeEntryCommand NewEntry(TestWorld world, int minutes, int daysBack = 1) =>
        new(world.ActiveUserId, world.ActiveMatterId, TestWorld.Today.AddDays(-daysBack), minutes, true, "Work.");

    /// <summary>Creates an isolated database with the fixture this file writes against.</summary>
    /// <param name="databaseName">Name for the isolated database.</param>
    /// <returns>Its connection string and the identifiers created in it.</returns>
    private async Task<(string ConnectionString, TestWorld World)> BuildWorldAsync(string databaseName)
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync(databaseName).ConfigureAwait(false);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var world = await TestWorld.CreateAsync(context).ConfigureAwait(false);

        return (connectionString, world);
    }

    /// <summary>Builds an authenticated client against a host bound to a specific database.</summary>
    /// <remarks>
    /// The host's clock is replaced with a fixed one so rule 4's window is anchored to a date the
    /// tests choose. Without it every date here would be relative to the real calendar and the
    /// suite would drift out of its own window as time passed.
    /// </remarks>
    /// <param name="connectionString">The database the host should use.</param>
    /// <returns>A client the caller owns and must dispose.</returns>
    private static HttpClient CreateClient(string connectionString)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                $"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Issuer", TokenFactory.Issuer);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Audience", TokenFactory.Audience);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:SigningKey", TokenFactory.SigningKey);
            builder.ConfigureServices(services =>
                services.AddSingleton<TimeProvider>(TestWorld.Clock));
        });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFactory.CreateValid());

        return client;
    }
}

/// <summary>
/// The smallest set of rows the write tests need: an active timekeeper, an active matter of an
/// active client, and an inactive matter to be refused by rule 5.
/// </summary>
/// <param name="ActiveUserId">A timekeeper who may record time.</param>
/// <param name="ActiveMatterId">An active matter of an active client.</param>
/// <param name="InactiveMatterId">An inactive matter, for rule 5's refusing case.</param>
internal sealed record TestWorld(int ActiveUserId, int ActiveMatterId, int InactiveMatterId)
{
    /// <summary>The rate the fixture's timekeeper bills at, and therefore what rule 6 must capture.</summary>
    public const decimal ActiveUserRate = 350.00m;

    /// <summary>The clock these tests run against, so rule 4's window never depends on the calendar.</summary>
    public static FixedClock Clock => FixedClock.Default;

    /// <summary>The date the tests treat as today.</summary>
    public static DateOnly Today => Clock.Today;

    /// <summary>Creates the rows in an isolated database.</summary>
    /// <param name="context">Context bound to that database.</param>
    /// <returns>The identifiers created.</returns>
    public static async Task<TestWorld> CreateAsync(LexTimeDbContext context)
    {
        var userId = await DirectSql.InsertUserAsync(context, "writer@lextime.test").ConfigureAwait(false);
        var clientId = await DirectSql.InsertClientAsync(context, "WRT").ConfigureAwait(false);
        var activeMatterId = await DirectSql.InsertMatterAsync(context, clientId, "M001").ConfigureAwait(false);
        var inactiveMatterId = await DirectSql.InsertMatterAsync(context, clientId, "M002").ConfigureAwait(false);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.Matters SET IsActive = 0 WHERE MatterId = @id;",
            new SqlParameter("@id", inactiveMatterId)).ConfigureAwait(false);

        return new TestWorld(userId, activeMatterId, inactiveMatterId);
    }
}
