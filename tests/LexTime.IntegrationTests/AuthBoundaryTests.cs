using System.Net;
using System.Net.Http.Headers;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the access boundary (FR-019, FR-020, SC-006): health and the documentation are
/// open, everything else is closed unless a valid token is presented.
/// </summary>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class AuthBoundaryTests(SqlServerFixture fixture)
{
    /// <summary>
    /// A protected route carrying a complete request, so the accepted case reaches real data.
    /// </summary>
    /// <remarks>
    /// Feature 001 pointed these tests at a <c>/api/v1/ping</c> placeholder, which was removed
    /// when this endpoint landed. Aiming them at the rollup strengthens them: a boundary proven
    /// only against a route that returns a constant is not proven against one that returns the
    /// database. The range is arbitrary and need not match any data — an empty report is still
    /// a 200, which is exactly what the accepted case needs to distinguish from a 401.
    /// </remarks>
    private const string ProtectedRoute =
        ReportEndpoints.WeeklyBillableRollupRoute + "?from=2026-01-05&to=2026-02-01";

    /// <summary>The health endpoint is reachable without credentials.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task HealthIsReachableWithoutAToken()
    {
        using var client = this.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A protected route with no token is refused.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ProtectedRouteIsRefusedWithoutAToken()
    {
        using var client = this.CreateClient();

        using var response = await client.GetAsync(new Uri(ProtectedRoute, UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tokens that are malformed, expired, or signed with a key the host does not trust are
    /// each refused with 401 rather than a server error.
    /// </summary>
    /// <param name="kind">Which invalid token to present.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Theory]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("wrongly-signed")]
    public async Task ProtectedRouteIsRefusedWithAnInvalidToken(string kind)
    {
        var token = kind switch
        {
            "malformed" => "not-a-token",
            "expired" => TokenFactory.CreateExpired(),
            _ => TokenFactory.CreateWronglySigned(),
        };

        using var client = this.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(new Uri(ProtectedRoute, UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A validly signed, unexpired token is accepted.
    /// </summary>
    /// <remarks>
    /// This matters as much as the refusals above. Without it, a route that rejected every
    /// request unconditionally — or a service that was simply broken — would satisfy all the
    /// other assertions in this class.
    /// </remarks>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ProtectedRouteIsAcceptedWithAValidToken()
    {
        using var client = this.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFactory.CreateValid());

        using var response = await client.GetAsync(new Uri(ProtectedRoute, UriKind.Relative))
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Builds a client against a host configured with the test signing key and the shared
    /// container's connection string.
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
