using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Application.Parties;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexTime.IntegrationTests;

/// <summary>Verifies timekeeper reads and the deliberate absence of write routes.</summary>
/// <param name="fixture">SQL Server fixture.</param>
[Collection(DatabaseCollection.Name)]
public sealed class TimekeeperEndpointTests(SqlServerFixture fixture)
{
    /// <summary>Lists and fetches a timekeeper including the current rate.</summary>
    [Fact]
    public async Task ListsAndFetchesTimekeepers()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_Timekeepers").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        var userId = await DirectSql.InsertUserAsync(context, "reader@lextime.test").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        var page = await client.GetFromJsonAsync<TimekeeperPage>("/api/v1/users?take=1").ConfigureAwait(true);
        var user = await client.GetFromJsonAsync<TimekeeperDto>($"/api/v1/users/{userId}").ConfigureAwait(true);

        Assert.Single(page!.Items);
        Assert.Equal(350.00m, user!.DefaultHourlyRate);
    }

    /// <summary>No POST or PUT route exists for timekeepers.</summary>
    [Fact]
    public async Task DoesNotServeTimekeeperWriteRoutes()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_TimekeeperWrites").ConfigureAwait(true);
        using var client = CreateClient(connectionString);

        using var post = await client.PostAsJsonAsync("/api/v1/users", new { email = "x@test", fullName = "X" }).ConfigureAwait(true);
        using var put = await client.PutAsJsonAsync("/api/v1/users/1", new { fullName = "X" }).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
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
