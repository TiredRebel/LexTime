using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Persistence operations needed by matter use cases.</summary>
public interface IMatterStore
{
    /// <summary>Finds a matter for a read or revision.</summary>
    /// <param name="matterId">Matter identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matter or null.</returns>
    Task<Matter?> FindAsync(int matterId, CancellationToken cancellationToken = default);

    /// <summary>Attempts to insert a matter.</summary>
    /// <param name="matter">Matter to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted matter.</returns>
    Task<Matter> AddAsync(Matter matter, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a tracked matter.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes after persistence.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists matters for one client.</summary>
    /// <param name="query">Client and page window.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Matching matters and total.</returns>
    Task<(IReadOnlyList<Matter> Items, int Total)> ListAsync(ListMattersQuery query, CancellationToken cancellationToken = default);
}
