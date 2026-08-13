namespace LexTime.Domain.Entities;

/// <summary>
/// A distinct piece of work carried out for exactly one client. Time is recorded against
/// a matter rather than against a client directly.
/// </summary>
public sealed class Matter
{
    /// <summary>Surrogate key assigned by the database.</summary>
    public int MatterId { get; set; }

    /// <summary>Identifier of the client this matter belongs to.</summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The firm's reference for this matter, unique <em>within its client</em> and not
    /// globally. Two different clients may each have a matter numbered <c>001</c>.
    /// </summary>
    public required string MatterNumber { get; set; }

    /// <summary>Short description of the work, as it appears on a report.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The billable flag applied to new entries on this matter unless the entry states
    /// otherwise. A pro bono matter defaults to non-billable; an individual entry may
    /// still differ.
    /// </summary>
    public bool IsBillableByDefault { get; set; }

    /// <summary>
    /// Whether new time may be recorded against this matter. Forward-looking only: closing
    /// a matter stops further recording and leaves its history intact.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>When the row was created, in UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The client this matter belongs to.</summary>
    public Client? Client { get; set; }

    /// <summary>Time entries recorded against this matter.</summary>
    public ICollection<TimeEntry> TimeEntries { get; } = [];
}
