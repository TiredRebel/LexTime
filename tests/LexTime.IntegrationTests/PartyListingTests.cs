using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.Parties;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.IntegrationTests;

/// <summary>Verifies status filtering, stable paging and client-scoped matter listing.</summary>
/// <param name="fixture">SQL Server fixture.</param>
[Collection(DatabaseCollection.Name)]
public sealed class PartyListingTests(SqlServerFixture fixture)
{
    /// <summary>Pages active clients without repeats and lists only a client's matters.</summary>
    [Fact]
    public async Task FiltersAndPagesClientsAndScopesMatters()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_PartyListing").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        var activeIds = new List<int>();
        for (var index = 0; index < 5; index++)
        {
            activeIds.Add(await DirectSql.InsertClientAsync(context, $"LIST-{index:000}").ConfigureAwait(true));
        }

        var inactiveId = await DirectSql.InsertClientAsync(context, "LIST-999").ConfigureAwait(true);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.Clients SET IsActive = 0 WHERE ClientId = @id;", new SqlParameter("@id", inactiveId)).ConfigureAwait(true);
        await DirectSql.InsertMatterAsync(context, activeIds[0], "001").ConfigureAwait(true);
        await DirectSql.InsertMatterAsync(context, activeIds[1], "001").ConfigureAwait(true);

        using var client = CreateClient(connectionString);
        var pages = new List<ClientDto>();
        for (var skip = 0; skip < 5; skip += 2)
        {
            var page = await client.GetFromJsonAsync<ClientPage>($"{ClientEndpoints.BaseRoute}?isActive=true&skip={skip}&take=2").ConfigureAwait(true);
            pages.AddRange(page!.Items);
        }

        Assert.Equal(5, pages.Count);
        Assert.Equal(5, pages.Select(item => item.ClientId).Distinct().Count());
        Assert.DoesNotContain(pages, item => item.ClientId == inactiveId);

        var matters = await client.GetFromJsonAsync<MatterPage>($"/api/v1/clients/{activeIds[0]}/matters").ConfigureAwait(true);
        Assert.Single(matters!.Items);
        Assert.Equal(activeIds[0], matters.Items[0].ClientId);
    }

    /// <summary>An omitted page size is bounded by the application default.</summary>
    [Fact]
    public async Task UsesTheDefaultPageBound()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_PartyDefaultPage").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        for (var index = 0; index < 55; index++)
        {
            await DirectSql.InsertClientAsync(context, $"PAGE-{index:000}").ConfigureAwait(true);
        }

        using var client = CreateClient(connectionString);
        var page = await client.GetFromJsonAsync<ClientPage>(ClientEndpoints.BaseRoute).ConfigureAwait(true);

        Assert.Equal(50, page!.Items.Count);
        Assert.Equal(55, page.Total);
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
