using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.Parties;
using LexTime.Application.TimeEntries;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.IntegrationTests;

/// <summary>HTTP contract tests for matter creation, composite uniqueness and the creation chain.</summary>
/// <param name="fixture">SQL Server fixture.</param>
[Collection(DatabaseCollection.Name)]
public sealed class MatterEndpointTests(SqlServerFixture fixture)
{
    /// <summary>The same number is allowed under different clients.</summary>
    [Fact]
    public async Task AllowsTheSameMatterNumberUnderDifferentClients()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_MatterComposite").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var first = await RegisterClientAsync(client, "MAT-001").ConfigureAwait(true);
        var second = await RegisterClientAsync(client, "MAT-002").ConfigureAwait(true);

        using var firstMatter = await OpenMatterAsync(client, first.ClientId, "001").ConfigureAwait(true);
        using var secondMatter = await OpenMatterAsync(client, second.ClientId, "001").ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Created, firstMatter.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondMatter.StatusCode);
    }

    /// <summary>The same number twice under one client returns 409.</summary>
    [Fact]
    public async Task RejectsARepeatedMatterNumberForOneClient()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_MatterConflict").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var owner = await RegisterClientAsync(client, "MAT-003").ConfigureAwait(true);
        using var first = await OpenMatterAsync(client, owner.ClientId, "001").ConfigureAwait(true);
        using var second = await OpenMatterAsync(client, owner.ClientId, "001").ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains($"Client {owner.ClientId}", await second.Content.ReadAsStringAsync().ConfigureAwait(true), StringComparison.Ordinal);
    }

    /// <summary>A missing parent is 404, and blank text is 400.</summary>
    [Fact]
    public async Task DistinguishesMissingParentAndMalformedMatter()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_MatterValidation").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var missing = await client.PostAsJsonAsync(
            $"/api/v1/clients/999999/matters", new OpenMatterCommand("001", "Matter", true)).ConfigureAwait(true);
        using var malformed = await client.PostAsJsonAsync(
            "/api/v1/clients/999999/matters", new OpenMatterCommand(" ", " ", true)).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    /// <summary>Client, matter and time-entry creation work as one usable chain.</summary>
    [Fact]
    public async Task CreatesAChainThatAcceptsTime()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_MatterChain").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        var userId = await DirectSql.InsertUserAsync(context, "matter-chain@lextime.test").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var owner = await RegisterClientAsync(client, "MAT-004").ConfigureAwait(true);
        using var matterResponse = await OpenMatterAsync(client, owner.ClientId, "001").ConfigureAwait(true);
        var matter = await matterResponse.Content.ReadFromJsonAsync<MatterDto>().ConfigureAwait(true);

        using var entry = await client.PostAsJsonAsync(
            TimeEntryEndpoints.BaseRoute,
            new RecordTimeEntryCommand(userId, matter!.MatterId, TestWorld.Today.AddDays(-1), 60, true, "Chain test.")).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
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
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(TestWorld.Clock));
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFactory.CreateValid());
        return client;
    }

    /// <summary>Registers a test client.</summary>
    /// <param name="client">Authenticated HTTP client.</param>
    /// <param name="code">Unique code.</param>
    /// <returns>Created DTO.</returns>
    private static async Task<ClientDto> RegisterClientAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand(code, code)).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientDto>().ConfigureAwait(true))!;
    }

    /// <summary>Opens a test matter.</summary>
    /// <param name="client">Authenticated HTTP client.</param>
    /// <param name="clientId">Owning client.</param>
    /// <param name="number">Matter number.</param>
    /// <returns>HTTP response owned by the caller.</returns>
    private static Task<HttpResponseMessage> OpenMatterAsync(HttpClient client, int clientId, string number) =>
        client.PostAsJsonAsync($"/api/v1/clients/{clientId}/matters", new OpenMatterCommand(number, "Matter", true));
}
