using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.Parties;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>HTTP contract tests for client registration, reads and uniqueness translation.</summary>
/// <param name="fixture">SQL Server fixture.</param>
[Collection(DatabaseCollection.Name)]
public sealed class ClientEndpointTests(SqlServerFixture fixture)
{
    /// <summary>Registers a client and returns its identifier and active state.</summary>
    [Fact]
    public async Task RegistersAndFetchesAnActiveClient()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_ClientCreate").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var created = await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand("CLI-001", "Client one")).ConfigureAwait(true);
        var dto = await created.Content.ReadFromJsonAsync<ClientDto>().ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(dto);
        Assert.True(dto.IsActive);
        Assert.Equal("CLI-001", dto.ClientCode);
        Assert.NotNull(created.Headers.Location);

        var fetched = await client.GetFromJsonAsync<ClientDto>($"{ClientEndpoints.BaseRoute}/{dto.ClientId}").ConfigureAwait(true);
        Assert.Equal(dto, fetched);
    }

    /// <summary>A missing client is reported as 404.</summary>
    [Fact]
    public async Task ReturnsNotFoundForMissingClient()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_ClientMissing").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client.GetAsync($"{ClientEndpoints.BaseRoute}/999999").ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Duplicate client codes, including a different case, return actionable 409 responses.</summary>
    [Fact]
    public async Task RejectsDuplicateClientCodeCaseInsensitively()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_ClientConflict").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand("CLI-002", "Client two")).ConfigureAwait(true);

        using var response = await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand("cli-002", "Another")).ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("clientCode", body, StringComparison.Ordinal);
        Assert.Contains("cli-002", body, StringComparison.Ordinal);
    }

    /// <summary>Malformed text is refused before a uniqueness check.</summary>
    [Fact]
    public async Task RejectsBlankClientFields()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_ClientValidation").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var response = await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand(" ", " ")).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The database constraint still refuses an outside collision after the API gained a translator.</summary>
    [Fact]
    public async Task DatabaseStillRejectsAnOutsideClientCollision()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_ClientConstraint").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        await DirectSql.InsertClientAsync(context, "CLI-003").ConfigureAwait(true);

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => DirectSql.InsertClientAsync(context, "CLI-003")).ConfigureAwait(true);
    }

    /// <summary>Creates an authenticated test client.</summary>
    /// <param name="connectionString">Target database.</param>
    /// <returns>An owned HTTP client.</returns>
    private static HttpClient CreateClient(string connectionString)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Issuer", TokenFactory.Issuer);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:Audience", TokenFactory.Audience);
            builder.UseSetting($"{AuthenticationSetup.SectionName}:SigningKey", TokenFactory.SigningKey);
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFactory.CreateValid());
        return client;
    }
}
