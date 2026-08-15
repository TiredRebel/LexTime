using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Read-only persistence operations for seeded timekeepers.</summary>
public interface ITimekeeperStore
{
    /// <summary>Finds a timekeeper.</summary>
    /// <param name="userId">Timekeeper identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The timekeeper or null.</returns>
    Task<User?> FindAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Lists a bounded timekeeper page.</summary>
    /// <param name="query">Page window.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Matching timekeepers and total.</returns>
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(ListTimekeepersQuery query, CancellationToken cancellationToken = default);
}
