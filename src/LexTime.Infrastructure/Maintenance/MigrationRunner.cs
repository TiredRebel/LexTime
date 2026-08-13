using LexTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Maintenance;

/// <summary>
/// Applies EF Core migrations from inside the application.
/// </summary>
/// <remarks>
/// This exists so the quickstart does not need the <c>dotnet-ef</c> global tool. That tool
/// is a separate install, and constitution P18 requires the quickstart to work from cold on
/// a machine with only Docker and the SDK — a prerequisite that is either met or not, with
/// no approximately. See specs/002-bootstrap-and-seed/research.md R0.
/// </remarks>
/// <param name="context">The context whose migrations are applied.</param>
public sealed class MigrationRunner(LexTimeDbContext context)
{
    /// <summary>
    /// Applies every pending migration. Running against an already-current database is a
    /// no-op, matching what <c>dotnet ef database update</c> guarantees.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of migrations applied, which is zero when already current.</returns>
    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false)).Count();

        if (pending > 0)
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        return pending;
    }

    /// <summary>
    /// Drops the database and rebuilds it from migrations.
    /// </summary>
    /// <remarks>
    /// Drops the <em>database</em>, not the container. The container keeps running and is
    /// never rebuilt (FR-006); discarding it and its storage is
    /// <c>docker compose down -v</c>, which the README documents rather than this code
    /// reimplementing (FR-008).
    /// </remarks>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of migrations applied to the rebuilt database.</returns>
    public async Task<int> ResetAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        return (await context.Database
            .GetAppliedMigrationsAsync(cancellationToken)
            .ConfigureAwait(false)).Count();
    }
}
