using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Registers clients and fetches a single client.</summary>
/// <param name="store">Client persistence port.</param>
/// <param name="clock">UTC clock for creation timestamps.</param>
public sealed class RegisterClientHandler(IClientStore store, TimeProvider clock)
{
    /// <summary>Creates an active client.</summary>
    /// <param name="command">Client code and name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created client or a translated conflict.</returns>
    public async Task<PartyWriteResult> HandleAsync(RegisterClientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var client = await store.AddAsync(new Client
            {
                ClientCode = command.ClientCode,
                Name = command.Name,
                IsActive = true,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            }, cancellationToken).ConfigureAwait(false);
            return PartyWriteResult.ClientSucceeded(client.ToDto());
        }
        catch (PartyConstraintConflictException exception)
        {
            return PartyWriteResult.Conflicted(exception.Conflict);
        }
    }
}

/// <summary>Fetches one client.</summary>
/// <param name="store">Client persistence port.</param>
public sealed class GetClientHandler(IClientStore store)
{
    /// <summary>Returns the client or null.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The client or null.</returns>
    public async Task<ClientDto?> HandleAsync(int clientId, CancellationToken cancellationToken = default) =>
        (await store.FindAsync(clientId, cancellationToken).ConfigureAwait(false))?.ToDto();
}

/// <summary>Changes mutable client fields.</summary>
/// <param name="store">Client persistence port.</param>
public sealed class ReviseClientHandler(IClientStore store)
{
    /// <summary>Revises a client or returns not found.</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="command">Mutable replacement values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The revised client or not found.</returns>
    public async Task<PartyWriteResult> HandleAsync(int clientId, ReviseClientCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var client = await store.FindAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return PartyWriteResult.Missing();
        }

        client.Name = command.Name;
        client.IsActive = command.IsActive;
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PartyWriteResult.ClientSucceeded(client.ToDto());
    }
}

/// <summary>Lists clients with optional status filtering.</summary>
/// <param name="store">Client persistence port.</param>
public sealed class ListClientsHandler(IClientStore store)
{
    /// <summary>Returns one bounded page.</summary>
    /// <param name="query">Filter and page window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The client page.</returns>
    public async Task<ClientPage> HandleAsync(ListClientsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var clamped = query.Clamped();
        var (items, total) = await store.ListAsync(clamped, cancellationToken).ConfigureAwait(false);
        return new(clamped.Skip, clamped.Take, total, [.. items.Select(item => item.ToDto())]);
    }
}
