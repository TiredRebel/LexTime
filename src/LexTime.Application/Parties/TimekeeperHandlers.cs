namespace LexTime.Application.Parties;

/// <summary>Reads seeded timekeepers.</summary>
/// <param name="store">Read-only timekeeper persistence port.</param>
public sealed class GetTimekeeperHandler(ITimekeeperStore store)
{
    /// <summary>Returns a timekeeper or null.</summary>
    /// <param name="userId">Timekeeper identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The timekeeper or null.</returns>
    public async Task<TimekeeperDto?> HandleAsync(int userId, CancellationToken cancellationToken = default) =>
        (await store.FindAsync(userId, cancellationToken).ConfigureAwait(false))?.ToDto();
}

/// <summary>Lists seeded timekeepers.</summary>
/// <param name="store">Read-only timekeeper persistence port.</param>
public sealed class ListTimekeepersHandler(ITimekeeperStore store)
{
    /// <summary>Returns one bounded page.</summary>
    /// <param name="query">Page window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The timekeeper page.</returns>
    public async Task<TimekeeperPage> HandleAsync(ListTimekeepersQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var clamped = query.Clamped();
        var (items, total) = await store.ListAsync(clamped, cancellationToken).ConfigureAwait(false);
        return new(clamped.Skip, clamped.Take, total, [.. items.Select(item => item.ToDto())]);
    }
}
