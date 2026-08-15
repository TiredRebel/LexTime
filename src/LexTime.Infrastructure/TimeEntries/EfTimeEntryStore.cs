using System.Data;
using LexTime.Application.TimeEntries;
using LexTime.Domain.Entities;
using LexTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexTime.Infrastructure.TimeEntries;

/// <summary>
/// The one implementation of <see cref="ITimeEntryStore"/>, over EF Core.
/// </summary>
/// <remarks>
/// EF Core owns writes and simple entity reads (constitution P5). The raw ADO.NET path belongs
/// only to reporting, and nothing here touches it.
/// </remarks>
/// <param name="context">The unit of work these operations share.</param>
public sealed class EfTimeEntryStore(LexTimeDbContext context) : ITimeEntryStore
{
    /// <inheritdoc />
    public Task<TimeEntry?> FindAsync(long timeEntryId, CancellationToken cancellationToken = default) =>
        context.TimeEntries.FirstOrDefaultAsync(e => e.TimeEntryId == timeEntryId, cancellationToken);

    /// <inheritdoc />
    public async Task<int> SumMinutesForUserOnDateAsync(
        int userId,
        DateOnly workDate,
        long? excludingEntryId,
        CancellationToken cancellationToken = default)
    {
        var query = context.TimeEntries
            .Where(e => e.UserId == userId && e.WorkDate == workDate);

        // The exclusion is what lets a duration be reduced. Without it, revising an entry of 600
        // minutes on a day already totalling 1440 would be measured against a total that still
        // includes the 600 being replaced, and a legitimate decrease would be refused.
        if (excludingEntryId is { } excluded)
        {
            query = query.Where(e => e.TimeEntryId != excluded);
        }

        // SumAsync over an empty set returns 0 for a non-nullable int projection, which is the
        // answer wanted here: a timekeeper with no entries that day has recorded no minutes.
        return await query.SumAsync(e => e.DurationMinutes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TimeEntryParties> FindPartiesAsync(
        int userId,
        int matterId,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Where(u => u.UserId == userId)
            .Select(u => new { u.IsActive, u.DefaultHourlyRate })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // The client's flag is projected alongside the matter's rather than fetched separately,
        // because rule 5 needs both and reporting only one of them makes the refusal unactionable.
        var matter = await context.Matters
            .Where(m => m.MatterId == matterId)
            .Select(m => new { m.IsActive, ClientIsActive = m.Client!.IsActive })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TimeEntryParties(
            UserExists: user is not null,
            UserIsActive: user?.IsActive ?? false,
            CurrentHourlyRate: user?.DefaultHourlyRate ?? 0m,
            MatterExists: matter is not null,
            MatterIsActive: matter?.IsActive ?? false,
            ClientIsActive: matter?.ClientIsActive ?? false);
    }

    /// <inheritdoc />
    public async Task<TimeEntry> AddAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        context.TimeEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry;
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        context.TimeEntries.Remove(entry);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<TimeEntry> Items, int Total)> ListAsync(
        ListTimeEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = context.TimeEntries.AsNoTracking().AsQueryable();

        if (query.UserId is { } userId)
        {
            filtered = filtered.Where(e => e.UserId == userId);
        }

        if (query.MatterId is { } matterId)
        {
            filtered = filtered.Where(e => e.MatterId == matterId);
        }

        if (query.From is { } from)
        {
            filtered = filtered.Where(e => e.WorkDate >= from);
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(e => e.WorkDate <= to);
        }

        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        // Ordered by identifier, never by work date. The seed holds thousands of entries per
        // date, so date alone is not a total order: two rows tie, the engine is free to return
        // them differently on the next request, and paging then drops one row and repeats
        // another. The identifier is unique and monotonic, so the order is total and stable.
        var items = await filtered
            .OrderBy(e => e.TimeEntryId)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<T> InSerializableTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Serialisable, not the default. Rule 3 reads the day's total and then writes, and two
        // requests that each pass on their own can both commit and leave a total above the
        // limit. Serialisable takes range locks on the read, so the second transaction blocks
        // until the first has finished rather than reading a total that is about to be stale.
        //
        // ponytail: coarser than it needs to be — there is no (UserId, WorkDate) index, so the
        // range lock covers more than the pair being written. Invisible at this write volume,
        // and an index is deliberately not added because it would perturb feature 004's
        // committed measurement. If contention ever matters, sp_getapplock keyed on the
        // timekeeper and date serialises exactly the pair instead.
        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        var result = await work(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }
}
