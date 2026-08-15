using LexTime.Domain.Entities;

namespace LexTime.Application.TimeEntries;

/// <summary>
/// A time entry as the API returns it.
/// </summary>
/// <param name="TimeEntryId">Its identifier, assigned when recorded and never reused.</param>
/// <param name="UserId">The timekeeper who recorded the work.</param>
/// <param name="MatterId">The matter the work was carried out for.</param>
/// <param name="WorkDate">The date the work is billed under, not the date it was typed in.</param>
/// <param name="DurationMinutes">Duration in whole minutes; always a positive multiple of six.</param>
/// <param name="IsBillable">Whether this time is charged to the client.</param>
/// <param name="HourlyRateSnapshot">
/// The rate captured when the entry was recorded. Returned rather than withheld so a caller need
/// not re-fetch to learn what was billed, and it is the field that makes an entry a historical
/// record rather than a projection — no later rate change touches it.
/// </param>
/// <param name="Narrative">The description of the work, as it would appear on an invoice.</param>
/// <param name="CreatedAtUtc">When the entry was recorded.</param>
/// <param name="UpdatedAtUtc">When it was last revised, or null if it never has been.</param>
public sealed record TimeEntryDto(
    long TimeEntryId,
    int UserId,
    int MatterId,
    DateOnly WorkDate,
    int DurationMinutes,
    bool IsBillable,
    decimal HourlyRateSnapshot,
    string Narrative,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Translation from the entity to what the API returns.</summary>
/// <remarks>
/// A hand-written extension beside the DTO it produces (constitution P4). Ten fields checked at
/// compile time, where a mapping library's failure mode is a runtime exception on a property
/// someone renamed.
/// </remarks>
public static class TimeEntryDtoExtensions
{
    /// <summary>Projects an entry onto its DTO.</summary>
    /// <param name="entry">The entry to project.</param>
    /// <returns>The DTO.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static TimeEntryDto ToDto(this TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new TimeEntryDto(
            entry.TimeEntryId,
            entry.UserId,
            entry.MatterId,
            entry.WorkDate,
            entry.DurationMinutes,
            entry.IsBillable,
            entry.HourlyRateSnapshot,
            entry.Narrative,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc);
    }
}
