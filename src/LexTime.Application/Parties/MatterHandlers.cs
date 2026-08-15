using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Opens and fetches matters.</summary>
/// <param name="store">Matter persistence port.</param>
/// <param name="clients">Client persistence port for parent resolution.</param>
/// <param name="clock">UTC clock for creation timestamps.</param>
public sealed class OpenMatterHandler(IMatterStore store, IClientStore clients, TimeProvider clock)
{
    /// <summary>Creates an active matter under an existing client.</summary>
    /// <param name="clientId">Owning client.</param>
    /// <param name="command">Matter values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created matter, a conflict, or not found.</returns>
    public async Task<PartyWriteResult> HandleAsync(int clientId, OpenMatterCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await clients.FindAsync(clientId, cancellationToken).ConfigureAwait(false) is null)
        {
            return PartyWriteResult.Missing();
        }

        try
        {
            var matter = await store.AddAsync(new Matter
            {
                ClientId = clientId,
                MatterNumber = command.MatterNumber,
                Name = command.Name,
                IsBillableByDefault = command.IsBillableByDefault,
                IsActive = true,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            }, cancellationToken).ConfigureAwait(false);
            return PartyWriteResult.MatterSucceeded(matter.ToDto());
        }
        catch (PartyConstraintConflictException exception)
        {
            return PartyWriteResult.Conflicted(exception.Conflict);
        }
    }
}

/// <summary>Fetches one matter.</summary>
/// <param name="store">Matter persistence port.</param>
public sealed class GetMatterHandler(IMatterStore store)
{
    /// <summary>Returns the matter or null.</summary>
    /// <param name="matterId">Matter identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matter or null.</returns>
    public async Task<MatterDto?> HandleAsync(int matterId, CancellationToken cancellationToken = default) =>
        (await store.FindAsync(matterId, cancellationToken).ConfigureAwait(false))?.ToDto();
}

/// <summary>Changes mutable matter fields.</summary>
/// <param name="store">Matter persistence port.</param>
public sealed class ReviseMatterHandler(IMatterStore store)
{
    /// <summary>Revises a matter or returns not found.</summary>
    /// <param name="matterId">Matter identifier.</param>
    /// <param name="command">Mutable replacement values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The revised matter or not found.</returns>
    public async Task<PartyWriteResult> HandleAsync(int matterId, ReviseMatterCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var matter = await store.FindAsync(matterId, cancellationToken).ConfigureAwait(false);
        if (matter is null)
        {
            return PartyWriteResult.Missing();
        }

        matter.Name = command.Name;
        matter.IsBillableByDefault = command.IsBillableByDefault;
        matter.IsActive = command.IsActive;
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PartyWriteResult.MatterSucceeded(matter.ToDto());
    }
}

/// <summary>Lists matters belonging to one client.</summary>
/// <param name="store">Matter persistence port.</param>
/// <param name="clients">Client persistence port for parent resolution.</param>
public sealed class ListMattersHandler(IMatterStore store, IClientStore clients)
{
    /// <summary>Returns a bounded matter page or not found for a missing client.</summary>
    /// <param name="query">Client and page window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A page, or null when the client does not exist.</returns>
    public async Task<MatterPage?> HandleAsync(ListMattersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (await clients.FindAsync(query.ClientId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var clamped = query.Clamped();
        var (items, total) = await store.ListAsync(clamped, cancellationToken).ConfigureAwait(false);
        return new(clamped.Skip, clamped.Take, total, [.. items.Select(item => item.ToDto())]);
    }
}
