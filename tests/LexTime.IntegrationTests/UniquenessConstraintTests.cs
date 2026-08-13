using Microsoft.Data.SqlClient;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the uniqueness rules (FR-010, SC-005), including the one most easily modelled
/// wrongly: matter numbers are unique within a client, not globally.
/// </summary>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class UniquenessConstraintTests(SqlServerFixture fixture)
{
    /// <summary>A duplicate client code is rejected.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task RejectsDuplicateClientCode()
    {
        await using var context = fixture.CreateContext();
        await DirectSql.InsertClientAsync(context, "UNIQ-A").ConfigureAwait(true);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            DirectSql.InsertClientAsync(context, "UNIQ-A")).ConfigureAwait(true);

        Assert.Contains("UX_Clients_ClientCode", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A duplicate timekeeper email is rejected.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task RejectsDuplicateUserEmail()
    {
        await using var context = fixture.CreateContext();
        await DirectSql.InsertUserAsync(context, "unique@lextime.test").ConfigureAwait(true);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            DirectSql.InsertUserAsync(context, "unique@lextime.test")).ConfigureAwait(true);

        Assert.Contains("UX_Users_Email", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same matter number is accepted under two different clients. A global unique index
    /// on <c>MatterNumber</c> would reject the second insert, and would do it during seeding
    /// rather than here.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task AcceptsSameMatterNumberUnderDifferentClients()
    {
        await using var context = fixture.CreateContext();
        var firstClient = await DirectSql.InsertClientAsync(context, "UNIQ-B").ConfigureAwait(true);
        var secondClient = await DirectSql.InsertClientAsync(context, "UNIQ-C").ConfigureAwait(true);

        await DirectSql.InsertMatterAsync(context, firstClient, "001").ConfigureAwait(true);
        await DirectSql.InsertMatterAsync(context, secondClient, "001").ConfigureAwait(true);
    }

    /// <summary>The same matter number twice under one client is rejected.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task RejectsSameMatterNumberUnderOneClient()
    {
        await using var context = fixture.CreateContext();
        var clientId = await DirectSql.InsertClientAsync(context, "UNIQ-D").ConfigureAwait(true);
        await DirectSql.InsertMatterAsync(context, clientId, "002").ConfigureAwait(true);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            DirectSql.InsertMatterAsync(context, clientId, "002")).ConfigureAwait(true);

        Assert.Contains(
            "UX_Matters_ClientId_MatterNumber", exception.Message, StringComparison.Ordinal);
    }
}
