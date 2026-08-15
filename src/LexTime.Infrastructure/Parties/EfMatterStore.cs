using LexTime.Application.Parties;
using LexTime.Domain.Entities;
using LexTime.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Parties;

/// <summary>EF Core implementation of the matter persistence port.</summary>
/// <param name="context">Request-scoped database context.</param>
public sealed class EfMatterStore(LexTimeDbContext context) : IMatterStore
{
    /// <inheritdoc />
    public Task<Matter?> FindAsync(int matterId, CancellationToken cancellationToken = default) =>
        context.Matters.FirstOrDefaultAsync(m => m.MatterId == matterId, cancellationToken);

    /// <inheritdoc />
    public async Task<Matter> AddAsync(Matter matter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matter);
        context.Matters.Add(matter);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await context.Entry(matter).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return matter;
        }
        catch (DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException sqlException &&
                UniqueConstraintTranslator.TryTranslate(sqlException, out var conflict) &&
                conflict is not null)
            {
                throw new PartyConstraintConflictException(conflict with { Value = matter.MatterNumber }, exception);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Matter> Items, int Total)> ListAsync(
        ListMattersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filtered = context.Matters.AsNoTracking().Where(m => m.ClientId == query.ClientId);
        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await filtered.OrderBy(m => m.MatterId)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
