using LexTime.Api.Maintenance;
using LexTime.Infrastructure;
using LexTime.Infrastructure.Maintenance;
using LexTime.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the host's command-line surface in
/// <c>specs/002-bootstrap-and-seed/contracts/host-cli.md</c>.
/// </summary>
/// <remarks>
/// Verbs are invoked through <see cref="MaintenanceCommands.RunAsync"/> against a service
/// provider rather than by spawning processes: the exit codes and the effects are the
/// contract, and a process boundary would add minutes per case without testing anything
/// extra.
/// <para>
/// Each test runs against its own database on the shared container, because it counts rows
/// and the constraint tests insert their own fixtures into the default one.
/// </para>
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class MaintenanceVerbTests(SqlServerFixture fixture)
{
    /// <summary>Reduced volumes, so a full seed runs in a test rather than in a minute.</summary>
    private static readonly SeedOptions SmallSeed = new()
    {
        UserCount = 8,
        ClientCount = 12,
        MatterCount = 30,
        TimeEntryCount = 4_000,
    };

    /// <summary>
    /// An unknown verb is refused with a message rather than treated as a request to start
    /// the web host.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task UnknownVerbIsRefused()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_UnknownVerb").ConfigureAwait(true);
        await using var provider = BuildProvider(connectionString);

        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["not-a-verb"]).ConfigureAwait(true);

        Assert.Equal(ExitCodes.OperationFailed, exitCode);
    }

    /// <summary>
    /// Arguments that are not a maintenance verb do not claim the process. This is what
    /// keeps the web host reachable when a host passes arguments of its own.
    /// </summary>
    [Fact]
    public void ArgumentsThatAreNotVerbsDoNotClaimTheProcess()
    {
        Assert.False(MaintenanceCommands.IsMaintenanceInvocation([]));
        Assert.False(MaintenanceCommands.IsMaintenanceInvocation(["--environment", "Development"]));
        Assert.True(MaintenanceCommands.IsMaintenanceInvocation(["state"]));
    }

    /// <summary>
    /// Migrating an already-current database applies nothing and reports success, matching
    /// what the tool it replaces guarantees.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task MigrateIsANoOpWhenAlreadyCurrent()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_Migrate").ConfigureAwait(true);
        await using var provider = BuildProvider(connectionString);

        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["migrate"]).ConfigureAwait(true);

        Assert.Equal(ExitCodes.Success, exitCode);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var applied = await context.Database.GetPendingMigrationsAsync().ConfigureAwait(true);
        Assert.Empty(applied);
    }

    /// <summary>
    /// Seeding an empty database loads exactly the requested number of entries, and every
    /// one of them satisfies the duration rules the schema enforces.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SeedLoadsTheRequestedVolume()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_Seed").ConfigureAwait(true);
        await using var provider = BuildProvider(connectionString);

        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["seed", "--entries", "4000"]).ConfigureAwait(true);

        Assert.Equal(ExitCodes.Success, exitCode);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        Assert.Equal(4_000, await context.TimeEntries.CountAsync().ConfigureAwait(true));
        Assert.Equal(0, await context.TimeEntries
            .CountAsync(e => e.DurationMinutes <= 0
                || e.DurationMinutes % 6 != 0
                || e.DurationMinutes > 1440)
            .ConfigureAwait(true));
    }

    /// <summary>
    /// Seeding a database that already holds data is refused rather than being additive
    /// (FR-003).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task SeedRefusesWhenTheDatabaseIsNotEmpty()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_SeedTwice").ConfigureAwait(true);
        await using var provider = BuildProvider(connectionString);

        await MaintenanceCommands
            .RunAsync(provider, ["seed", "--entries", "1000"]).ConfigureAwait(true);

        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["seed", "--entries", "1000"]).ConfigureAwait(true);

        Assert.Equal(ExitCodes.DatabaseNotEmpty, exitCode);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        Assert.Equal(1_000, await context.TimeEntries.CountAsync().ConfigureAwait(true));
    }

    /// <summary>
    /// An empty procedure directory is a normal state. It is the only state this feature
    /// will ever see, so it is the default path rather than an edge case (FR-010).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ApplyProceduresSucceedsWithAnEmptyDirectory()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_Procedures").ConfigureAwait(true);
        await using var provider = BuildProvider(connectionString);

        var exitCode = await MaintenanceCommands
            .RunAsync(provider, ["apply-procedures"]).ConfigureAwait(true);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>
    /// Builds a provider equivalent to the host's, bound to a specific database.
    /// </summary>
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

    /// <summary>Exposes the reduced volumes to other tests in this assembly.</summary>
    /// <returns>The small seed options.</returns>
    internal static SeedOptions SmallSeedOptions() => SmallSeed;
}
