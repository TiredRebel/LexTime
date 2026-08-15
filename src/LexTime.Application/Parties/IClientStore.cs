using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Persistence operations needed by client use cases.</summary>
/// <remarks>This is the third aggregate-specific port required by the layered design.</remarks>
public interface IClientStore
{
    /// <summary>Finds a client for a read or revision.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The client or null.</returns>
    Task<Client?> FindAsync(int clientId, CancellationToken cancellationToken = default);

    /// <summary>Attempts to insert a client.</summary>
    /// <param name="client">Client to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted client.</returns>
    Task<Client> AddAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a tracked client.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes after persistence.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists a bounded client page.</summary>
    /// <param name="query">Filter and page window.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Matching clients and total.</returns>
    Task<(IReadOnlyList<Client> Items, int Total)> ListAsync(ListClientsQuery query, CancellationToken cancellationToken = default);
}
