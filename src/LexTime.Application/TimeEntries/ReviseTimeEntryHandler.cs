using LexTime.Domain.Rules;

namespace LexTime.Application.TimeEntries;

/// <summary>
/// Corrects an existing entry, if every applicable rule permits it.
/// </summary>
/// <remarks>
/// "Applicable" is the word doing the work. Rules 1, 2, 3 and 6 always apply; rule 4 applies only
/// when the work date is being changed and rule 5 only when the matter is. An update that leaves
/// a field alone is not a submission of that field — so an entry recorded 200 days ago can still
/// have its narrative corrected, and still cannot have its date moved.
/// </remarks>
/// <param name="store">Supplies the entry, the facts, and the write.</param>
/// <param name="clock">Supplies today's date for rule 4.</param>
public sealed class ReviseTimeEntryHandler(ITimeEntryStore store, TimeProvider clock)
{
    /// <summary>
    /// Applies the revision, or returns why it was refused.
    /// </summary>
    /// <remarks>
    /// A refused revision leaves the stored entry exactly as it was (FR-015). That is not
    /// achieved by care but by order: nothing is assigned to the entity until every rule has
    /// passed, so there is no partially applied state for a refusal to leave behind.
    /// </remarks>
    /// <param name="timeEntryId">Which entry to revise.</param>
    /// <param name="command">The revised values. Carries no timekeeper and no rate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The revised entry, the rules that refused it, or a missing entry or party.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public async Task<TimeEntryWriteResult> HandleAsync(
        long timeEntryId,
        ReviseTimeEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await store.InSerializableTransactionAsync(
            async token =>
            {
                var entry = await store.FindAsync(timeEntryId, token).ConfigureAwait(false);
                if (entry is null)
                {
                    return TimeEntryWriteResult.EntryMissing();
                }

                var parties = await store
                    .FindPartiesAsync(entry.UserId, command.MatterId, token)
                    .ConfigureAwait(false);

                if (!parties.MatterExists)
                {
                    return TimeEntryWriteResult.PartyMissing();
                }

                // The two flags the clarification turns on. "Being changed" means "differs from
                // what is stored" — with a PUT every field arrives, so absence cannot mean
                // untouched and a comparison is the only honest test. It also removes any way for
                // a caller to declare a field unchanged when it is not.
                var workDateChanged = command.WorkDate != entry.WorkDate;
                var matterChanged = command.MatterId != entry.MatterId;

                var otherMinutes = await store
                    .SumMinutesForUserOnDateAsync(entry.UserId, command.WorkDate, entry.TimeEntryId, token)
                    .ConfigureAwait(false);

                var violations = TimeEntryRuleSet.Evaluate(new TimeEntryFacts(
                    command.DurationMinutes,
                    command.WorkDate,
                    DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    otherMinutes,
                    parties.MatterIsActive,
                    parties.ClientIsActive,

                    // The timekeeper is not revisable, so their status is not re-litigated here:
                    // an entry recorded by someone who has since left is still correctable.
                    TimekeeperIsActive: true,
                    EvaluateWorkDate: workDateChanged,
                    EvaluateMatter: matterChanged));

                if (violations.Count > 0)
                {
                    return TimeEntryWriteResult.Refused(violations);
                }

                entry.MatterId = command.MatterId;
                entry.WorkDate = command.WorkDate;
                entry.DurationMinutes = command.DurationMinutes;
                entry.IsBillable = command.IsBillable;
                entry.Narrative = command.Narrative;
                entry.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;

                // HourlyRateSnapshot is conspicuously absent, and that is rule 6. Rebuilding the
                // entity from the command and re-reading the timekeeper's current rate would
                // rewrite history on every edit, silently, and only the rule-6 test would notice.
                await store.SaveChangesAsync(token).ConfigureAwait(false);

                return TimeEntryWriteResult.Success(entry.ToDto());
            },
            cancellationToken).ConfigureAwait(false);
    }
}
