namespace LexTime.Application.TimeEntries;

/// <summary>Removes an entry outright.</summary>
/// <remarks>
/// No rule gates deletion. `docs/prd.md` §2.2 rules out an approval or locking workflow, and
/// refusing to remove an entry because its work date has aged past the backdating window would
/// be the first half of one (FR-017).
/// </remarks>
/// <param name="store">Supplies the entry and takes the removal.</param>
public sealed class DeleteTimeEntryHandler(ITimeEntryStore store)
{
    /// <summary>Deletes the entry, if it exists.</summary>
    /// <param name="timeEntryId">Which entry to remove.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or that there was no such entry.</returns>
    public async Task<TimeEntryWriteResult> HandleAsync(
        long timeEntryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await store.FindAsync(timeEntryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return TimeEntryWriteResult.EntryMissing();
        }

        await store.RemoveAsync(entry, cancellationToken).ConfigureAwait(false);

        return TimeEntryWriteResult.Success(entry.ToDto());
    }
}

/// <summary>Fetches one entry by identifier.</summary>
/// <param name="store">Supplies the entry.</param>
public sealed class GetTimeEntryHandler(ITimeEntryStore store)
{
    /// <summary>Fetches the entry.</summary>
    /// <param name="timeEntryId">Which entry.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry with its captured rate, or null when the identifier matches none.</returns>
    public async Task<TimeEntryDto?> HandleAsync(
        long timeEntryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await store.FindAsync(timeEntryId, cancellationToken).ConfigureAwait(false);

        return entry?.ToDto();
    }
}

/// <summary>Lists entries matching a set of filters, one page at a time.</summary>
/// <param name="store">Supplies the page and the total.</param>
public sealed class ListTimeEntriesHandler(ITimeEntryStore store)
{
    /// <summary>Returns one page of matching entries.</summary>
    /// <remarks>
    /// The page window is clamped here rather than at the endpoint, so every caller of this use
    /// case gets the same bounds — including one that is not an HTTP request. An unfiltered
    /// request is still bounded and never returns the whole table (FR-020).
    /// </remarks>
    /// <param name="query">The filters and requested window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, with the total matching the filters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public async Task<TimeEntryPage> HandleAsync(
        ListTimeEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clamped = query.Clamped();
        var (items, total) = await store.ListAsync(clamped, cancellationToken).ConfigureAwait(false);

        return new TimeEntryPage(
            clamped.Skip,
            clamped.Take,
            total,
            [.. items.Select(e => e.ToDto())]);
    }
}
