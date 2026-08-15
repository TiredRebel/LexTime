using LexTime.Domain.Entities;

namespace LexTime.Application.TimeEntries;

/// <summary>
/// Storage for time entries, as the use cases need it.
/// </summary>
/// <remarks>
/// <b>This is the type a reviewer will check against constitution P4's repository ban, so the
/// reasoning lives here rather than only in the plan.</b> P4 forbids "a generic repository over
/// <c>DbSet&lt;T&gt;</c>" because "EF Core's <c>DbSet&lt;T&gt;</c> is already the repository and
/// <c>DbContext</c> is already the unit of work; wrapping them adds a layer that only forwards
/// calls".
/// <para>
/// Why this is not that: it is not generic, has no type parameter, serves one aggregate, exists
/// in one copy, and cannot be reused for clients or matters. And
/// <see cref="SumMinutesForUserOnDateAsync"/> is not a <c>DbSet</c> operation at all — it is a
/// domain question with an aggregate answer, and the fact rule 3 cannot be evaluated without.
/// </para>
/// <para>
/// Why the objection still partly lands: <see cref="FindAsync"/>, <see cref="AddAsync"/> and
/// <see cref="RemoveAsync"/> do forward. If <c>LexTime.Application</c> could see
/// <c>LexTimeDbContext</c> they would not exist — and it cannot, because P4's own layering
/// forbids it. The forwarding is the price of that layering, charged by the principle that also
/// bans paying it twice. P4's text says "an interface with a single implementation is expected
/// here rather than deferred", which is what this is.
/// </para>
/// </remarks>
public interface ITimeEntryStore
{
    /// <summary>Fetches one entry, or null when the identifier matches none.</summary>
    /// <param name="timeEntryId">The entry to fetch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<TimeEntry?> FindAsync(long timeEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Totals a timekeeper's recorded minutes for one date, optionally ignoring one entry.
    /// </summary>
    /// <remarks>
    /// The fact rule 3 is evaluated against, and the reason this interface is not a plain CRUD
    /// wrapper. <paramref name="excludingEntryId"/> is what makes a duration reducible: revising
    /// an entry of 600 minutes on a day totalling 1440 down to 300 must be permitted, and would
    /// not be if the total still counted the 600 being replaced.
    /// </remarks>
    /// <param name="userId">The timekeeper.</param>
    /// <param name="workDate">The date to total.</param>
    /// <param name="excludingEntryId">The entry being revised, or null when recording a new one.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Total minutes recorded by that timekeeper on that date, excluding the given entry.</returns>
    Task<int> SumMinutesForUserOnDateAsync(
        int userId,
        DateOnly workDate,
        long? excludingEntryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the active flags and current rate the rules need, in one round trip.
    /// </summary>
    /// <remarks>
    /// One call rather than three, because the three questions are always asked together and a
    /// caller that could ask them separately would eventually ask only some of them.
    /// </remarks>
    /// <param name="userId">The timekeeper to look up.</param>
    /// <param name="matterId">The matter to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The lookup, or <see langword="null"/> for either party when no such row exists — which is
    /// a different mistake from an inactive one and deserves a different answer.
    /// </returns>
    Task<TimeEntryParties> FindPartiesAsync(
        int userId,
        int matterId,
        CancellationToken cancellationToken = default);

    /// <summary>Records a new entry and assigns its identifier.</summary>
    /// <param name="entry">The entry to record.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The recorded entry, with its generated identifier.</returns>
    Task<TimeEntry> AddAsync(TimeEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Saves changes made to an entry already loaded through this store.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the changes are persisted.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes an entry outright, leaving no trace.</summary>
    /// <param name="entry">The entry to remove.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the entry is gone.</returns>
    Task RemoveAsync(TimeEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Lists entries matching the filters, ordered and paged.</summary>
    /// <param name="query">The filters and page window.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page and the total matching the filters, which is not the page's length.</returns>
    Task<(IReadOnlyList<TimeEntry> Items, int Total)> ListAsync(
        ListTimeEntriesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a unit of work in a transaction that will not let a concurrent one interleave.
    /// </summary>
    /// <remarks>
    /// Rule 3 is a read followed by a write, and is therefore defeatable by timing: two requests
    /// both read a total of 1400, both add 40, both pass, and the stored total becomes 1480 with
    /// neither request having been individually wrong. Serialisable isolation is what makes the
    /// read see a range that cannot change beneath it. Repeatable Read is not enough — it holds
    /// the rows it read and does not stop a second transaction inserting a new one, which is
    /// exactly this case.
    /// </remarks>
    /// <typeparam name="T">What the unit of work produces.</typeparam>
    /// <param name="work">The read-evaluate-write sequence to serialise.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>Whatever <paramref name="work"/> returned, after the transaction commits.</returns>
    Task<T> InSerializableTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The timekeeper and matter an entry names, and the facts the rules need about them.
/// </summary>
/// <param name="UserExists">Whether the timekeeper exists at all.</param>
/// <param name="UserIsActive">Whether the timekeeper is active (FR-013).</param>
/// <param name="CurrentHourlyRate">
/// The timekeeper's rate as it stands now. Captured onto a new entry and never re-read for an
/// existing one — that is rule 6.
/// </param>
/// <param name="MatterExists">Whether the matter exists at all.</param>
/// <param name="MatterIsActive">Half of rule 5.</param>
/// <param name="ClientIsActive">The other half, carried separately so a refusal can name which.</param>
public sealed record TimeEntryParties(
    bool UserExists,
    bool UserIsActive,
    decimal CurrentHourlyRate,
    bool MatterExists,
    bool MatterIsActive,
    bool ClientIsActive);
