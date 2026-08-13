using LexTime.Infrastructure.Persistence;
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
    public LexTimeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LexTimeDbContext>()
            .UseSqlServer(this.ConnectionString)
            .Options;

        return new LexTimeDbContext(options);
    }
}
