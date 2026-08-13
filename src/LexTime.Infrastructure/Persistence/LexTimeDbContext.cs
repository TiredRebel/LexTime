using System.Reflection;
using LexTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Persistence;

/// <summary>
/// The EF Core context for the LexTime schema. Owns writes and simple entity reads;
/// reporting reads go through stored procedures invoked directly, never through this
/// context (constitution P5).
/// </summary>
/// <param name="options">Provider and connection options supplied by dependency injection.</param>
public sealed class LexTimeDbContext(DbContextOptions<LexTimeDbContext> options)
    : DbContext(options)
{
    /// <summary>Timekeepers. Seeded and read-only through the API.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Organisations the firm bills.</summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Pieces of work, each belonging to exactly one client.</summary>
    public DbSet<Matter> Matters => Set<Matter>();

    /// <summary>Recorded blocks of work. The table the reporting path aggregates.</summary>
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    /// <summary>
    /// Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> in this assembly.
    /// Discovery rather than explicit registration, so that adding a configuration file is
    /// enough to have it take effect and there is no second list to keep in step.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
