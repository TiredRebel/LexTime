using LexTime.Domain.Entities;
using LexTime.Domain.Rules;

namespace LexTime.Application.TimeEntries;

/// <summary>
/// Records new time, if every rule permits it.
/// </summary>
/// <remarks>
/// One handler class for one use case (constitution P4). It gathers the facts the rules need,
/// asks the rules, and persists — it does not decide anything a rule decides. **No limit or
/// threshold appears anywhere in this file**: if this handler needed to know that a day holds
/// 1440 minutes, that number would exist in two places and the two would eventually disagree.
/// </remarks>
/// <param name="store">Supplies the facts and takes the write.</param>
/// <param name="clock">
/// Supplies today's date for rule 4. Injected rather than read, so a test can state what day it
/// is instead of waiting for one.
/// </param>
public sealed class RecordTimeEntryHandler(ITimeEntryStore store, TimeProvider clock)
{
    /// <summary>
    /// Records the submitted entry, or returns why it was refused.
    /// </summary>
    /// <remarks>
    /// The whole sequence — read the day's total, evaluate, insert — runs inside one serialisable
    /// transaction. Without that, two requests that each pass rule 3 on their own can both commit
    /// and leave a total above the daily maximum, with neither request having been wrong and
    /// nothing having failed.
    /// </remarks>
    /// <param name="command">What to record. Carries no rate; the rate is captured here.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The recorded entry, the rules that refused it, or a missing party.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public async Task<TimeEntryWriteResult> HandleAsync(
        RecordTimeEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await store.InSerializableTransactionAsync(
            async token =>
            {
                var parties = await store
                    .FindPartiesAsync(command.UserId, command.MatterId, token)
                    .ConfigureAwait(false);

                // A party that does not exist is not a rule violation. Reporting "the matter is
                // not active" for a matter that was never there sends the caller to fix a matter
                // they do not have.
                if (!parties.UserExists || !parties.MatterExists)
                {
                    return TimeEntryWriteResult.PartyMissing();
                }

                var otherMinutes = await store
                    .SumMinutesForUserOnDateAsync(command.UserId, command.WorkDate, null, token)
                    .ConfigureAwait(false);

                var violations = TimeEntryRuleSet.Evaluate(new TimeEntryFacts(
                    command.DurationMinutes,
                    command.WorkDate,
                    DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    otherMinutes,
                    parties.MatterIsActive,
                    parties.ClientIsActive,
                    parties.UserIsActive));

                if (violations.Count > 0)
                {
                    return TimeEntryWriteResult.Refused(violations);
                }

                var entry = new TimeEntry
                {
                    UserId = command.UserId,
                    MatterId = command.MatterId,
                    WorkDate = command.WorkDate,
                    DurationMinutes = command.DurationMinutes,
                    IsBillable = command.IsBillable,

                    // Rule 6. Read from the timekeeper now and never re-read: a later rate change
                    // must not rewrite the value of work already recorded.
                    HourlyRateSnapshot = parties.CurrentHourlyRate,

                    Narrative = command.Narrative,
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                };

                var recorded = await store.AddAsync(entry, token).ConfigureAwait(false);

                return TimeEntryWriteResult.Success(recorded.ToDto());
            },
            cancellationToken).ConfigureAwait(false);
    }
}
