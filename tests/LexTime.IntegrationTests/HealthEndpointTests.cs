using System.Net;
using System.Text.Json;
using LexTime.Api.Authentication;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the health endpoint contract in <c>specs/001-solution-and-schema/contracts/health.md</c>.
/// </summary>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class HealthEndpointTests(SqlServerFixture fixture)
{
    /// <summary>
    /// A connection string pointing at a port nothing listens on, with a short connect
    /// timeout so the failing check returns inside the five seconds SC-004 allows.
    /// </summary>
    private const string UnreachableConnectionString =
        "Server=localhost,14330;Database=LexTime;User Id=sa;Password=irrelevant;" +
        "TrustServerCertificate=True;Encrypt=False;Connect Timeout=2";

    /// <summary>
    /// With the database reachable, the endpoint returns 200, reports Healthy overall, and
    /// names the database check individually (FR-023, FR-024, FR-025).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsHealthyAndNamesTheDatabaseCheck_WhenDatabaseIsReachable()
    {
        using var factory = CreateFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative))
            .ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());

        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToList();
        var database = Assert.Single(checks, c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("Healthy", database.GetProperty("status").GetString());
    }

    /// <summary>
    /// With the database unreachable, the endpoint returns 503 and names the database check
    /// as the failing one (FR-024, FR-025, FR-026).
    /// </summary>
    /// <remarks>
    /// This is the assertion that catches a check which only constructs a connection object.
    /// Construction succeeds against a server that is not running, so such a check reports
    /// Healthy here and a naive test would pass.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsUnhealthyAndNamesTheDatabaseCheck_WhenDatabaseIsUnreachable()
    {
        using var factory = CreateFactory(UnreachableConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative))
            .ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("Unhealthy", document.RootElement.GetProperty("status").GetString());

        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToList();
        var database = Assert.Single(checks, c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("Unhealthy", database.GetProperty("status").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(database.GetProperty("description").GetString()),
            "A failing check must describe its failure; the caller has no log access.");
    }

    /// <summary>
    /// The health response discloses nothing about the connection, because the endpoint is
    /// unauthenticated and anything it returns is public (FR-027).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DoesNotLeakConnectionDetails_WhenTheDatabaseIsUnreachable()
    {
        using var factory = CreateFactory(UnreachableConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative))
            .ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a host bound to a specific connection string.
    /// </summary>
    /// <param name="connectionString">The connection the health check should probe.</param>
    /// <returns>A factory the caller owns and must dispose.</returns>
    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                $"ConnectionStrings:{DependencyInjection.ConnectionStringName}",
                connectionString);

            // Token validation is configured at startup and throws if incomplete, so even
            // a test that never presents a token has to supply these.
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Issuer", TokenFactory.Issuer);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Audience", TokenFactory.Audience);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:SigningKey", TokenFactory.SigningKey);
        });
}
