using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Application.Reporting;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>
/// Exercises the rollup over HTTP: the envelope, the client filter, validation and the
/// empty cases.
/// </summary>
/// <remarks>
/// These run against the shared seeded-shape database and assert the endpoint's behaviour, not
/// the report's arithmetic. The arithmetic is pinned in
/// <see cref="WeeklyBillableRollupTests"/>, which calls the procedure directly against a
/// hand-computed fixture — a test routed through HTTP asserts the procedure, the reader, the
/// handler, the endpoint and the serialiser at once, and names none of them when it fails.
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class RollupEndpointTests(SqlServerFixture fixture)
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/v1/reports/weekly-billable-rollup";

    /// <summary>
    /// A valid request returns the envelope, echoes the range, and carries populated rows.
    /// </summary>
    /// <remarks>
    /// Also pins the range-edge rule: the first reported week has no prior week inside the
    /// range, so its change is absent rather than a number.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsTheRollupForARangeWithActivity()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupEndpoint").ConfigureAwait(true);

        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await RollupFixtureBuilder.PopulateAsync(context).ConfigureAwait(true);
        }

        using var client = CreateClient(connectionString);

        var response = await client
            .GetFromJsonAsync<WeeklyBillableRollupResponse>(
                $"{Route}?from={RollupFixtureBuilder.RangeStart:yyyy-MM-dd}&to={RollupFixtureBuilder.RangeEnd:yyyy-MM-dd}")
            .ConfigureAwait(true);

        Assert.NotNull(response);
        Assert.Equal(RollupFixtureBuilder.RangeStart, response.From);
        Assert.Equal(RollupFixtureBuilder.RangeEnd, response.To);
        Assert.NotEmpty(response.Rows);

        var first = response.Rows[0];
        Assert.NotEqual(0, first.IsoYear);
        Assert.InRange(first.IsoWeek, 1, 53);
        Assert.False(string.IsNullOrWhiteSpace(first.ClientCode));
        Assert.False(string.IsNullOrWhiteSpace(first.ClientName));
        Assert.True(first.ClientRankInWeek >= 1);

        // The range begins on the fixture's first Monday, so nothing precedes it that the
        // report is allowed to see.
        Assert.All(
            response.Rows.Where(r => r.WeekStartDate == RollupFixtureBuilder.RangeStart),
            row => Assert.Null(row.HoursDeltaVsPriorWeek));

        // ...and something after it must carry a number, or the null above would be
        // indistinguishable from a column that is never populated at all.
        Assert.Contains(response.Rows, r => r.HoursDeltaVsPriorWeek is not null);
    }

    /// <summary>
    /// The single-client filter narrows the rows and leaves every figure inside them intact.
    /// </summary>
    /// <remarks>
    /// The standing is the assertion that matters. Ranking after filtering would give every
    /// row a standing of 1 and would still look plausible in a response body, which is why
    /// this compares the filtered rows against the unfiltered ones rather than checking the
    /// column is merely present (FR-012).
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FilteringToOneClientPreservesItsStandingAmongAllClients()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupFilter").ConfigureAwait(true);

        int quietClientId;
        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            var built = await RollupFixtureBuilder.PopulateAsync(context).ConfigureAwait(true);
            quietClientId = built.SmallClientId;
        }

        using var client = CreateClient(connectionString);

        var all = await client
            .GetFromJsonAsync<WeeklyBillableRollupResponse>(
                $"{Route}?from={RollupFixtureBuilder.RangeStart:yyyy-MM-dd}&to={RollupFixtureBuilder.RangeEnd:yyyy-MM-dd}")
            .ConfigureAwait(true);

        var filtered = await client
            .GetFromJsonAsync<WeeklyBillableRollupResponse>(
                $"{Route}?from={RollupFixtureBuilder.RangeStart:yyyy-MM-dd}&to={RollupFixtureBuilder.RangeEnd:yyyy-MM-dd}&clientId={quietClientId}")
            .ConfigureAwait(true);

        Assert.NotNull(all);
        Assert.NotNull(filtered);
        Assert.NotEmpty(filtered.Rows);
        Assert.All(filtered.Rows, row => Assert.Equal(quietClientId, row.ClientId));

        // This client is deliberately not the busiest, so a rank of 1 everywhere would be the
        // symptom of ranking after filtering rather than a legitimate result.
        Assert.Contains(filtered.Rows, row => row.ClientRankInWeek > 1);

        // Every filtered row must be identical to its unfiltered counterpart, figure for
        // figure — the filter selects rows, it does not recompute them.
        foreach (var row in filtered.Rows)
        {
            var unfiltered = all.Rows.Single(r =>
                r.ClientId == row.ClientId && r.WeekStartDate == row.WeekStartDate);

            Assert.Equal(unfiltered, row);
        }
    }

    /// <summary>
    /// A range containing no activity returns success with no rows, not an error.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsAnEmptyResultForARangeWithNoActivity()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupEmptyRange").ConfigureAwait(true);

        using var client = CreateClient(connectionString);

        var response = await client
            .GetFromJsonAsync<WeeklyBillableRollupResponse>($"{Route}?from=2030-01-07&to=2030-02-04")
            .ConfigureAwait(true);

        Assert.NotNull(response);
        Assert.Empty(response.Rows);
    }

    /// <summary>
    /// A client identifier matching nothing returns an empty result rather than 404.
    /// </summary>
    /// <remarks>
    /// The report is over a period. A client with no activity in that period legitimately
    /// produces nothing, and 404 would claim the report itself did not exist (FR-020).
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsAnEmptyResultForAClientThatMatchesNothing()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupUnknownClient").ConfigureAwait(true);

        using var client = CreateClient(connectionString);

        using var response = await client
            .GetAsync(new Uri(
                $"{Route}?from={RollupFixtureBuilder.RangeStart:yyyy-MM-dd}&to={RollupFixtureBuilder.RangeEnd:yyyy-MM-dd}&clientId=999999",
                UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<WeeklyBillableRollupResponse>().ConfigureAwait(true);

        Assert.NotNull(body);
        Assert.Empty(body.Rows);
    }

    /// <summary>
    /// An inverted range is refused, and the refusal names both dates.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefusesARangeWhoseStartIsAfterItsEnd()
    {
        using var client = CreateClient(fixture.ConnectionString);

        using var response = await client
            .GetAsync(new Uri($"{Route}?from=2026-03-29&to=2026-01-05", UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Contains("2026-03-29", body, StringComparison.Ordinal);
        Assert.Contains("2026-01-05", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A missing date is refused rather than defaulted.
    /// </summary>
    /// <param name="queryString">A range with one end omitted.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Theory]
    [InlineData("?to=2026-03-29")]
    [InlineData("?from=2026-01-05")]
    [InlineData("")]
    public async Task RefusesARequestMissingADate(string queryString)
    {
        using var client = CreateClient(fixture.ConnectionString);

        using var response = await client
            .GetAsync(new Uri(Route + queryString, UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Builds a client against a host bound to a specific database and the test signing key.
    /// </summary>
    /// <param name="connectionString">The database the host should read from.</param>
    /// <returns>An authenticated HTTP client the caller owns and must dispose.</returns>
    private static HttpClient CreateClient(string connectionString)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                $"ConnectionStrings:{DependencyInjection.ConnectionStringName}",
                connectionString);
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
