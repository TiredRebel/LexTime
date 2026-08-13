using LexTime.Infrastructure.Maintenance;
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
    /// Starts the container, brings the schema up to the latest migration, and applies the
    /// source-controlled stored procedures.
    /// </summary>
    /// <returns>A task that completes once the database is ready to be queried.</returns>
    public async Task InitializeAsync()
    {
        await this.container.StartAsync().ConfigureAwait(false);

        await using (var context = this.CreateContext())
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await ApplyProceduresAsync(this.ConnectionString).ConfigureAwait(false);
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

        await using (var context = CreateContext(connectionString))
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await ApplyProceduresAsync(connectionString).ConfigureAwait(false);

        return connectionString;
    }

    /// <summary>
    /// Applies every procedure under <c>db/programmability</c> to the given database.
    /// </summary>
    /// <remarks>
    /// Migrations alone were sufficient while that directory was empty. From feature 003 it is
    /// not: without this step every rollup test fails with "could not find stored procedure",
    /// which points at the fixture rather than at anything real.
    /// </remarks>
    /// <param name="connectionString">The database to apply them to.</param>
    /// <returns>A task that completes once every procedure has been applied.</returns>
    private static async Task ApplyProceduresAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await new ProcedureApplier(context)
            .ApplyAllAsync(FindRepositoryRoot())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Walks up from the test assembly's location looking for the solution file.
    /// </summary>
    /// <remarks>
    /// <c>db/programmability</c> is a repository path and the tests run from
    /// <c>bin/Debug/net9.0</c>. Anchored on <c>LexTime.sln</c> rather than a fixed number of
    /// parent hops, so it survives a change of output path.
    /// <para>
    /// This duplicates the walk <c>MaintenanceCommands</c> does privately. Deliberate: six
    /// lines with no state, against widening the API project's public surface for a third
    /// caller that does not exist. If one appears, that is the point to extract it.
    /// </para>
    /// </remarks>
    /// <returns>The repository root, or the current directory if no marker was found.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LexTime.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
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
