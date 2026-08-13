namespace LexTime.Domain.Entities;

/// <summary>
/// A timekeeper: a person whose recorded time is billed to clients.
/// </summary>
/// <remarks>
/// Users are seeded and read-only through the API for the life of this project. There is
/// no registration, no identity provider and no user management (see docs/prd.md §2.2).
/// </remarks>
public sealed class User
{
    /// <summary>Surrogate key assigned by the database.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// The timekeeper's email address, unique across all users. Used as the natural
    /// identifier when correlating seeded data with external systems.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>Display name as it should appear on a report.</summary>
    public required string FullName { get; set; }

    /// <summary>
    /// The rate in USD applied to new time entries at the moment they are created.
    /// Changing this does not alter entries already recorded: each entry keeps the rate
    /// captured at its creation in <see cref="TimeEntry.HourlyRateSnapshot"/>.
    /// </summary>
    public decimal DefaultHourlyRate { get; set; }

    /// <summary>
    /// Whether this timekeeper may have new time recorded against them. Forward-looking
    /// only — setting it false does not remove, hide or invalidate entries recorded while
    /// it was true.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>When the row was created, in UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Time entries recorded by this timekeeper.</summary>
    public ICollection<TimeEntry> TimeEntries { get; } = [];
}
