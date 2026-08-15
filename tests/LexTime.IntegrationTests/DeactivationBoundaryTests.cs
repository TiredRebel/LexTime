using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LexTime.Api.Authentication;
using LexTime.Api.Endpoints;
using LexTime.Application.Parties;
using LexTime.Application.Reporting;
using LexTime.Application.TimeEntries;
using LexTime.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.IntegrationTests;

/// <summary>Verifies deactivation across the write and reporting feature boundaries.</summary>
/// <param name="fixture">SQL Server fixture.</param>
[Collection(DatabaseCollection.Name)]
public sealed class DeactivationBoundaryTests(SqlServerFixture fixture)
{
    /// <summary>Closing a matter refuses new time while preserving the old rollup row.</summary>
    [Fact]
    public async Task MatterDeactivationStopsNewTimeAndPreservesHistory()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_DeactivationMatter").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        var userId = await DirectSql.InsertUserAsync(context, "deactivation@lextime.test").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var owner = await RegisterClientAsync(client, "DEACT-001").ConfigureAwait(true);
        using var opened = await client.PostAsJsonAsync(
            $"/api/v1/clients/{owner.ClientId}/matters", new OpenMatterCommand("001", "Matter", true)).ConfigureAwait(true);
        var matter = (await opened.Content.ReadFromJsonAsync<MatterDto>().ConfigureAwait(true))!;
        var date = TestWorld.Today.AddDays(-3);

        using var recorded = await client.PostAsJsonAsync(
            TimeEntryEndpoints.BaseRoute,
            new RecordTimeEntryCommand(userId, matter.MatterId, date, 60, true, "Before closure.")).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        using var closed = await client.PutAsJsonAsync(
            $"/api/v1/matters/{matter.MatterId}", new ReviseMatterCommand("Matter", true, false)).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        using var refused = await client.PostAsJsonAsync(
            TimeEntryEndpoints.BaseRoute,
            new RecordTimeEntryCommand(userId, matter.MatterId, date, 60, true, "After closure.")).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("matter is not active", await refused.Content.ReadAsStringAsync().ConfigureAwait(true), StringComparison.Ordinal);

        var reportBefore = await GetReportAsync(client, date).ConfigureAwait(true);
        var rowBefore = Assert.Single(reportBefore.Rows, row => row.ClientId == owner.ClientId);

        using var reportAfterClose = await client.GetAsync(ReportUri(date)).ConfigureAwait(true);
        var reportAfter = await reportAfterClose.Content.ReadFromJsonAsync<WeeklyBillableRollupResponse>().ConfigureAwait(true);
        var rowAfter = Assert.Single(reportAfter!.Rows, row => row.ClientId == owner.ClientId);
        Assert.Equal(rowBefore.BillableHours, rowAfter.BillableHours);
    }

    /// <summary>Closing a client leaves its matters open and produces the client refusal branch.</summary>
    [Fact]
    public async Task ClientDeactivationDoesNotCascadeToMatters()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_DeactivationClient").ConfigureAwait(true);
        await using var context = SqlServerFixture.CreateContext(connectionString);
        var userId = await DirectSql.InsertUserAsync(context, "client-deactivation@lextime.test").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var owner = await RegisterClientAsync(client, "DEACT-002").ConfigureAwait(true);
        using var opened = await client.PostAsJsonAsync(
            $"/api/v1/clients/{owner.ClientId}/matters", new OpenMatterCommand("001", "Matter", true)).ConfigureAwait(true);
        var matter = (await opened.Content.ReadFromJsonAsync<MatterDto>().ConfigureAwait(true))!;

        using var closed = await client.PutAsJsonAsync(
            $"/api/v1/clients/{owner.ClientId}", new ReviseClientCommand("Client", false)).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var listed = await client.GetFromJsonAsync<MatterPage>($"/api/v1/clients/{owner.ClientId}/matters").ConfigureAwait(true);
        Assert.True(listed!.Items.Single().IsActive);

        using var refused = await client.PostAsJsonAsync(
            TimeEntryEndpoints.BaseRoute,
            new RecordTimeEntryCommand(userId, matter.MatterId, TestWorld.Today.AddDays(-1), 60, true, "Client closed.")).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("client is not", await refused.Content.ReadAsStringAsync().ConfigureAwait(true), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reactivation and repeated state requests succeed, and a malformed update is atomic.</summary>
    [Fact]
    public async Task SupportsBothDirectionsAndLeavesARefusedUpdateUntouched()
    {
        var connectionString = await fixture.CreateIsolatedDatabaseAsync("LexTime_DeactivationStates").ConfigureAwait(true);
        using var client = CreateClient(connectionString);
        var owner = await RegisterClientAsync(client, "DEACT-003").ConfigureAwait(true);
        using var closed = await client.PutAsJsonAsync($"/api/v1/clients/{owner.ClientId}", new ReviseClientCommand("Client", false)).ConfigureAwait(true);
        using var reopened = await client.PutAsJsonAsync($"/api/v1/clients/{owner.ClientId}", new ReviseClientCommand("Client", true)).ConfigureAwait(true);
        using var repeated = await client.PutAsJsonAsync($"/api/v1/clients/{owner.ClientId}", new ReviseClientCommand("Client", true)).ConfigureAwait(true);
        using var refused = await client.PutAsJsonAsync($"/api/v1/clients/{owner.ClientId}", new ReviseClientCommand(" ", false)).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var final = await client.GetFromJsonAsync<ClientDto>($"/api/v1/clients/{owner.ClientId}").ConfigureAwait(true);
        Assert.Equal(owner with { IsActive = true }, final);
    }

    /// <summary>Registers a client used by a boundary test.</summary>
    /// <param name="client">Authenticated HTTP client.</param>
    /// <param name="code">Unique client code.</param>
    /// <returns>Created client.</returns>
    private static async Task<ClientDto> RegisterClientAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync(ClientEndpoints.BaseRoute, new RegisterClientCommand(code, "Client")).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientDto>().ConfigureAwait(true))!;
    }

    /// <summary>Reads a report over the week containing a date.</summary>
    /// <param name="client">Authenticated HTTP client.</param>
    /// <param name="date">Date with activity.</param>
    /// <returns>The report response.</returns>
    private static async Task<WeeklyBillableRollupResponse> GetReportAsync(HttpClient client, DateOnly date) =>
        (await client.GetFromJsonAsync<WeeklyBillableRollupResponse>(ReportUri(date)).ConfigureAwait(true))!;

    /// <summary>Builds the report URI for the week containing a date.</summary>
    /// <param name="date">Date with activity.</param>
    /// <returns>Report URI.</returns>
    private static string ReportUri(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        var from = date.AddDays(-daysFromMonday);
        return $"{ReportEndpoints.WeeklyBillableRollupRoute}?from={from:yyyy-MM-dd}&to={from.AddDays(6):yyyy-MM-dd}";
    }

    /// <summary>Creates an authenticated client with the feature test clock.</summary>
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
}
