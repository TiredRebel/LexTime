namespace LexTime.Domain.Entities;

/// <summary>
/// A block of work recorded by one timekeeper against one matter on one date. The unit the
/// entire reporting path aggregates.
/// </summary>
public sealed class TimeEntry
{
    /// <summary>
    /// Surrogate key assigned by the database. A 64-bit key because this table is expected
    /// to hold hundreds of thousands of rows per year and is the only one that grows
    /// without bound.
    /// </summary>
    public long TimeEntryId { get; set; }

    /// <summary>Identifier of the timekeeper who recorded the work.</summary>
    public int UserId { get; set; }

    /// <summary>Identifier of the matter the work was carried out for.</summary>
    public int MatterId { get; set; }

    /// <summary>
    /// The date the work is billed under, which is not necessarily the date the entry was
    /// typed in. Reporting groups by this date.
    /// </summary>
    /// <remarks>
    /// Deliberately carries no database constraint. The rule limiting how far an entry may
    /// be backdated governs what may be <em>submitted</em> through the API; it is not an
    /// invariant on history already recorded. A constraint here would reject seeded
    /// history and would make the database progressively reject its own contents as time
    /// passed. See feature 001 FR-012.
    /// </remarks>
    public DateOnly WorkDate { get; set; }

    /// <summary>
    /// Duration in whole minutes. Legal billing works in six-minute increments — a tenth
    /// of an hour — so this is always a positive multiple of six and never exceeds 1440,
    /// the number of minutes in a day. The database enforces all three conditions.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Whether this time is charged to the client. Defaults from the matter but may differ
    /// per entry: writing off an hour on a billable matter is an ordinary act.
    /// </summary>
    public bool IsBillable { get; set; }

    /// <summary>
    /// The timekeeper's hourly rate as it stood when this entry was created, in USD.
    /// Copied rather than referenced, so that changing a rate does not silently rewrite
    /// the value of work already recorded.
    /// </summary>
    public decimal HourlyRateSnapshot { get; set; }

    /// <summary>Free-text description of the work, as it would appear on an invoice.</summary>
    public required string Narrative { get; set; }

    /// <summary>When the row was created, in UTC. Distinct from <see cref="WorkDate"/>.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When the row was last modified, in UTC, or null if never modified.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>The timekeeper who recorded the work.</summary>
    public User? User { get; set; }

    /// <summary>The matter the work was carried out for.</summary>
    public Matter? Matter { get; set; }
}
