using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.TimeEntries;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>
/// Filtering and paging over the seeded dataset.
/// </summary>
/// <remarks>
/// Read-only, and the plain half of this feature — `docs/prd.md` §7 names endpoints like these as
/// the first thing to cut if time runs short. The one assertion here that is not routine is the
/// paging one: ordering by work date would read better and would be wrong, because the seed holds
/// thousands of entries per date and a non-total order lets successive pages drop one row and
/// repeat another.
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container, seeded by other tests' needs.</param>
[Collection(DatabaseCollection.Name)]
public sealed class TimeEntryListingTests(SqlServerFixture fixture)
{
    /// <summary>The route under test.</summary>
    private const string Route = TimeEntryEndpoints.BaseRoute;

    /// <summary>Filtering by timekeeper returns only that timekeeper's entries.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FiltersByTimekeeper()
    {
        var (connectionString, world) = await this.BuildAsync("LexTime_ListByUser", entries: 12).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var page = await client
            .GetFromJsonAsync<TimeEntryPage>($"{Route}?userId={world.ActiveUserId}")
            .ConfigureAwait(true);

        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, e => Assert.Equal(world.ActiveUserId, e.UserId));
    }

    /// <summary>Matter and date filters combine.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CombinesMatterAndDateFilters()
    {
        var (connectionString, world) = await this.BuildAsync("LexTime_ListCombined", entries: 12).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var from = TestWorld.Today.AddDays(-5);
        var page = await client
            .GetFromJsonAsync<TimeEntryPage>(
                $"{Route}?matterId={world.ActiveMatterId}&from={from:yyyy-MM-dd}&to={TestWorld.Today:yyyy-MM-dd}")
            .ConfigureAwait(true);

        Assert.NotNull(page);
        Assert.All(page.Items, e => Assert.Equal(world.ActiveMatterId, e.MatterId));
        Assert.All(page.Items, e => Assert.InRange(e.WorkDate, from, TestWorld.Today));
    }

    /// <summary>
    /// Paging visits every matching entry exactly once.
    /// </summary>
    /// <remarks>
    /// The assertion that would fail under an unstable order. Ordering by work date alone leaves
    /// ties the engine may resolve differently between requests, and a row can then be skipped on
    /// one page and repeated on the next — with the page counts still looking right.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PagesWithoutSkippingOrRepeatingAnEntry()
    {
        var (connectionString, world) = await this.BuildAsync("LexTime_ListPaging", entries: 12).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var seen = new List<long>();
        for (var skip = 0; skip < 12; skip += 4)
        {
            var page = await client
                .GetFromJsonAsync<TimeEntryPage>($"{Route}?userId={world.ActiveUserId}&skip={skip}&take=4")
                .ConfigureAwait(true);

            Assert.NotNull(page);
            Assert.Equal(12, page.Total);
            seen.AddRange(page.Items.Select(e => e.TimeEntryId));
        }

        Assert.Equal(12, seen.Count);
        Assert.Equal(12, seen.Distinct().Count());
    }

    /// <summary>An unfiltered request is bounded by the default page size.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BoundsAnUnfilteredRequestByTheDefaultPageSize()
    {
        var (connectionString, _) = await this.BuildAsync("LexTime_ListUnbounded", entries: 60).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var page = await client.GetFromJsonAsync<TimeEntryPage>(Route).ConfigureAwait(true);

        Assert.NotNull(page);
        Assert.Equal(ListTimeEntriesQuery.DefaultTake, page.Take);
        Assert.True(page.Items.Count <= ListTimeEntriesQuery.DefaultTake);
        Assert.Equal(60, page.Total);
    }

    /// <summary>An oversized page request is clamped rather than honoured.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ClampsAnOversizedPageRequest()
    {
        var (connectionString, _) = await this.BuildAsync("LexTime_ListClamp", entries: 5).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var page = await client.GetFromJsonAsync<TimeEntryPage>($"{Route}?take=100000").ConfigureAwait(true);

        Assert.NotNull(page);
        Assert.Equal(ListTimeEntriesQuery.MaximumTake, page.Take);
    }

    /// <summary>A range with nothing in it is an empty page, not an error.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsAnEmptyPageForARangeWithNoEntries()
    {
        var (connectionString, _) = await this.BuildAsync("LexTime_ListEmpty", entries: 3).ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var page = await client
            .GetFromJsonAsync<TimeEntryPage>($"{Route}?from=2030-01-01&to=2030-01-31")
            .ConfigureAwait(true);

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    /// <summary>Creates an isolated database with a known number of entries in it.</summary>
    /// <param name="databaseName">Name for the isolated database.</param>
    /// <param name="entries">How many entries to record, spread across recent dates.</param>
    /// <returns>Its connection string and the identifiers created.</returns>
    private async Task<(string ConnectionString, TestWorld World)> BuildAsync(string databaseName, int entries)
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync(databaseName).ConfigureAwait(false);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var world = await TestWorld.CreateAsync(context).ConfigureAwait(false);

        // Spread across several dates so the ordering assertion is not trivially satisfied by
        // every row sharing a date — which is precisely the case where date ordering breaks.
        for (var i = 0; i < entries; i++)
        {
            await DirectSql.InsertTimeEntryAsync(
                context,
                world.ActiveUserId,
                world.ActiveMatterId,
                60,
                TestWorld.Today.AddDays(-(i % 5))).ConfigureAwait(false);
        }

        return (connectionString, world);
    }

    /// <summary>Builds an authenticated client bound to a specific database.</summary>
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
        });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFactory.CreateValid());

        return client;
    }
}
