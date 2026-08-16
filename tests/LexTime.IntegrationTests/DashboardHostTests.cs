using System.Net;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the host boundary introduced by the dashboard: its static shell is open while its
/// source report remains protected.
/// </summary>
/// <param name="fixture">Supplies the real SQL Server container used by the hosted API.</param>
[Collection(DatabaseCollection.Name)]
public sealed class DashboardHostTests(SqlServerFixture fixture)
{
    /// <summary>
    /// The dashboard document is reachable without credentials so it can present the token
    /// prompt instead of turning a missing session into a blank 401 response.
    /// </summary>
    /// <returns>A task that completes when the host contract has been asserted.</returns>
    [Fact]
    public async Task DashboardShellIsReachableWithoutAToken()
    {
        using var client = this.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative))
            .ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("LexTime", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Serving the anonymous shell does not weaken the fallback-closed policy on the report
    /// whose figures it displays.
    /// </summary>
    /// <returns>A task that completes when the authorization boundary has been asserted.</returns>
    [Fact]
    public async Task RollupRemainsProtectedWithoutAToken()
    {
        using var client = this.CreateClient();
        var route = ReportEndpoints.WeeklyBillableRollupRoute
            + "?from=2026-06-18&to=2026-08-13";

        using var response = await client.GetAsync(new Uri(route, UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Builds a client against the real container with the signing configuration expected by
    /// the application's closed authorization policy.
    /// </summary>
    /// <returns>An HTTP client the caller owns and must dispose.</returns>
    private HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                $"ConnectionStrings:{DependencyInjection.ConnectionStringName}",
                fixture.ConnectionString);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Issuer", TokenFactory.Issuer);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Audience", TokenFactory.Audience);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:SigningKey", TokenFactory.SigningKey);
        });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}
