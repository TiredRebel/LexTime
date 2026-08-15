using LexTime.Application.Parties;
using LexTime.Domain.Entities;
using LexTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.Parties;

/// <summary>Read-only EF Core implementation for seeded timekeepers.</summary>
/// <param name="context">Request-scoped database context.</param>
public sealed class EfTimekeeperStore(LexTimeDbContext context) : ITimekeeperStore
{
    /// <inheritdoc />
    public Task<User?> FindAsync(int userId, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(
        ListTimekeepersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filtered = context.Users.AsNoTracking();
        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await filtered.OrderBy(u => u.UserId)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
