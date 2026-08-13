using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LexTime.IntegrationTests;

/// <summary>
/// Starts a real SQL Server 2022 container for the duration of the test run and applies
/// the migrations to it.
/// </summary>
/// <remarks>
/// Constitution P11 forbids an in-memory or file-based provider anywhere in this project.
/// The rules under test here — a check constraint, a composite unique index, the deliberate
/// absence of a date constraint — are properties of the database engine, and a fake
/// provider would report whatever the fake was written to believe.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>
    /// Connection string for the running container, valid only after
    /// <see cref="InitializeAsync"/> has completed.
    /// </summary>
    public string ConnectionString => this.container.GetConnectionString();

    /// <summary>
    /// Starts the container and brings the schema up to the latest migration.
    /// </summary>
    /// <returns>A task that completes once the database is migrated and reachable.</returns>
    public async Task InitializeAsync()
    {
        await this.container.StartAsync().ConfigureAwait(false);

        await using var context = this.CreateContext();
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>Stops and removes the container.</summary>
    /// <returns>A task that completes once the container has been disposed.</returns>
    public async Task DisposeAsync() =>
        await this.container.DisposeAsync().ConfigureAwait(false);

    /// <summary>
    /// Creates a context against the container. Callers own the returned instance and are
    /// responsible for disposing it.
    /// </summary>
    /// <returns>A new <see cref="LexTimeDbContext"/> bound to the container.</returns>
    public LexTimeDbContext CreateContext() => CreateContext(this.ConnectionString);

    /// <summary>
    /// Builds a connection string for a separate database on the same container.
    /// </summary>
    /// <param name="databaseName">Name of the database to point at.</param>
    /// <returns>A connection string differing only in the initial catalogue.</returns>
    public string ConnectionStringFor(string databaseName) =>
        new SqlConnectionStringBuilder(this.ConnectionString)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;

    /// <summary>
    /// Drops, recreates and migrates a named database on the shared container.
    /// </summary>
    /// <remarks>
    /// Seeding tests count rows and compare them against expected volumes, so they cannot
    /// share a database with the constraint tests, which insert their own fixtures into it.
    /// A separate database on the same container costs a few seconds; a separate container
    /// costs tens of them.
    /// </remarks>
    /// <param name="databaseName">Name of the database to create.</param>
    /// <returns>A connection string for the freshly migrated database.</returns>
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName)
    {
        await this.container
            .ExecScriptAsync(
                $"IF DB_ID('{databaseName}') IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{databaseName}]; END; CREATE DATABASE [{databaseName}];")
            .ConfigureAwait(false);

        var connectionString = this.ConnectionStringFor(databaseName);

        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync().ConfigureAwait(false);

        return connectionString;
    }

    /// <summary>Creates a context bound to an explicit connection string.</summary>
    /// <param name="connectionString">The connection to bind to.</param>
    /// <returns>A new context the caller owns.</returns>
    public static LexTimeDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LexTimeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LexTimeDbContext(options);
    }
}
