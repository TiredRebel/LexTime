using LexTime.Api.Maintenance;
using LexTime.Infrastructure;
using LexTime.Infrastructure.Maintenance;
using LexTime.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the three-state classification in research.md R6.
/// </summary>
/// <remarks>
/// <see cref="SeedState.Partial"/> is the state these tests exist for. A seed interrupted
/// midway leaves a database that looks populated and is not; a check asking only "are there
/// any rows" reports it complete, and the rollup would then report on it faithfully and
/// produce totals that are wrong.
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class DatabaseStateTests(SqlServerFixture fixture)
{
    private static readonly SeedOptions Small = new()
    {
        UserCount = 6,
        ClientCount = 10,
        MatterCount = 20,
        TimeEntryCount = 500,
    };

    /// <summary>A migrated database with no rows reports Empty.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ReportsEmptyForAFreshlyMigratedDatabase()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_StateEmpty").ConfigureAwait(true);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var report = await new DatabaseStateInspector(context)
            .InspectAsync(Small).ConfigureAwait(true);

        Assert.Equal(SeedState.Empty, report.State);
    }

    /// <summary>A fully seeded database reports Complete against the volumes used to seed it.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ReportsCompleteAfterAFullSeed()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_StateComplete").ConfigureAwait(true);

        await using (var seedContext = SqlServerFixture.CreateContext(connectionString))
        {
            await new BulkSeeder(seedContext).SeedAsync(Small).ConfigureAwait(true);
        }

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var report = await new DatabaseStateInspector(context)
            .InspectAsync(Small).ConfigureAwait(true);

        Assert.Equal(SeedState.Complete, report.State);
        Assert.Equal(Small.TimeEntryCount, report.TimeEntries);
    }

    /// <summary>
    /// A database seeded at one volume and judged against another reports Partial. This is
    /// the shape an interrupted seed leaves behind.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ReportsPartialWhenCountsDoNotMatchTheExpectedVolumes()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_StatePartial").ConfigureAwait(true);

        await using (var seedContext = SqlServerFixture.CreateContext(connectionString))
        {
            await new BulkSeeder(seedContext).SeedAsync(Small).ConfigureAwait(true);
        }

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var report = await new DatabaseStateInspector(context)
            .InspectAsync(Small with { TimeEntryCount = Small.TimeEntryCount * 2 })
            .ConfigureAwait(true);

        Assert.Equal(SeedState.Partial, report.State);
    }

    /// <summary>
    /// Verification exits non-zero and names the failing check when a band is missed
    /// (FR-023).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task VerifySeedFailsWhenABandIsMissed()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_VerifySkewed").ConfigureAwait(true);

        // Seeded, then deliberately skewed: every entry made non-billable pushes the
        // non-billable share to 100%, far outside its 10-25% band.
        await using (var seedContext = SqlServerFixture.CreateContext(connectionString))
        {
            await new BulkSeeder(seedContext).SeedAsync(Small).ConfigureAwait(true);
            await seedContext.Database
                .ExecuteSqlRawAsync("UPDATE dbo.TimeEntries SET IsBillable = 0;")
                .ConfigureAwait(true);
        }

        await using var provider = BuildProvider(connectionString);
        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["verify-seed", "--entries", Small.TimeEntryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)])
            .ConfigureAwait(true);

        Assert.Equal(ExitCodes.VerificationFailed, exitCode);
    }

    /// <summary>Builds a provider bound to a specific database.</summary>
    /// <param name="connectionString">The database to work against.</param>
    /// <returns>A provider the caller owns and must dispose.</returns>
    private static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
            })
            .Build();

        return new ServiceCollection()
            .AddLexTimeInfrastructure(configuration)
            .BuildServiceProvider();
    }
}
