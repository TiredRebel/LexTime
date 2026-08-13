using LexTime.Infrastructure.Maintenance;
using LexTime.Infrastructure.Persistence;
using LexTime.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexTime.Infrastructure;

/// <summary>
/// Registration entry point for the persistence layer. One extension method per layer, so
/// that composition in <c>Program.cs</c> reads as a table of contents (constitution P21).
/// </summary>
public static class DependencyInjection
{
    /// <summary>Configuration key holding the database connection string.</summary>
    public const string ConnectionStringName = "LexTime";

    /// <summary>
    /// Registers the <see cref="LexTimeDbContext"/> against the configured SQL Server
    /// connection.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">Configuration supplying the connection string.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// The <c>LexTime</c> connection string is missing or empty. Thrown at startup rather
    /// than on first query, so a misconfigured environment fails immediately and says why.
    /// </exception>
    public static IServiceCollection AddLexTimeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it in " +
                "appsettings.Development.json or the ConnectionStrings__LexTime environment variable.");
        }

        services.AddDbContext<LexTimeDbContext>(options => options.UseSqlServer(connectionString));

        // Maintenance operations invoked by the bootstrap script through the host's
        // command-line surface. Scoped, because each depends on the scoped DbContext.
        services.AddScoped<MigrationRunner>();
        services.AddScoped<DatabaseStateInspector>();
        services.AddScoped<ProcedureApplier>();
        services.AddScoped<BulkSeeder>();
        services.AddScoped<SeedVerifier>();
        services.AddSingleton(new DevelopmentTokenMinter(configuration));

        return services;
    }
}
