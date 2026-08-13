using System.Data.Common;
using LexTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexTime.Api.HealthChecks;

/// <summary>
/// Reports whether the database is genuinely usable, by executing a query against it.
/// </summary>
/// <remarks>
/// Executing rather than connecting is the whole point (FR-026). Constructing a connection
/// object succeeds against a server that is not running, so a check that stops short of a
/// query reports healthy while the database is down — and a test written against such a
/// check passes.
/// </remarks>
/// <param name="dbContext">The context supplying the connection to probe.</param>
public sealed class DatabaseHealthCheck(LexTimeDbContext dbContext) : IHealthCheck
{
    /// <summary>
    /// How long the probe query may take before the check is treated as failed. Short
    /// enough that a failure surfaces inside the five seconds SC-004 allows, rather than
    /// sitting on the provider's default timeout.
    /// </summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Runs the probe.</summary>
    /// <param name="context">Registration metadata supplied by the health check service.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>
    /// Healthy if the query executed; Unhealthy with a short description otherwise. The
    /// description names the class of failure and never includes the connection string,
    /// credentials or a stack trace (FR-027).
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(ProbeTimeout);
            await dbContext.Database
                .ExecuteSqlRawAsync("SELECT 1", cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy("Query executed successfully.");
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            // Deliberately not ex.ToString() and not the exception object: this endpoint is
            // unauthenticated, so the description is public. A provider's message can name
            // a host, so the text is fixed rather than forwarded.
            return HealthCheckResult.Unhealthy("The database did not respond to a query.");
        }
    }
}
