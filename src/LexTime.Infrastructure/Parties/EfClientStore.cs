using LexTime.Application.Parties;
using LexTime.Domain.Entities;
using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Parties;

/// <summary>EF Core implementation of the client persistence port.</summary>
/// <param name="context">Request-scoped database context.</param>
public sealed class EfClientStore(LexTimeDbContext context) : IClientStore
{
    /// <inheritdoc />
    public Task<Client?> FindAsync(int clientId, CancellationToken cancellationToken = default) =>
        context.Clients.FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<Client> AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        context.Clients.Add(client);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await context.Entry(client).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch (DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException sqlException &&
                UniqueConstraintTranslator.TryTranslate(sqlException, out var conflict) &&
                conflict is not null)
            {
                throw new PartyConstraintConflictException(conflict with { Value = client.ClientCode }, exception);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Client> Items, int Total)> ListAsync(
        ListClientsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filtered = context.Clients.AsNoTracking().AsQueryable();

        if (query.IsActive is { } isActive)
        {
            filtered = filtered.Where(c => c.IsActive == isActive);
        }

        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await filtered.OrderBy(c => c.ClientId)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
