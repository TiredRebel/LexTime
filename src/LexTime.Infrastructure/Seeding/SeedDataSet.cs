using LexTime.Domain.Entities;

namespace LexTime.Infrastructure.Seeding;

/// <summary>
/// A generated dataset, held in memory before it is loaded.
/// </summary>
/// <remarks>
/// Entities carry no database keys yet: identity values are assigned by SQL Server when
/// each table lands. Time entries therefore reference their user and matter by
/// <em>index into these lists</em>, and the loader translates those to real keys once the
/// parent rows exist. Keeping generation ignorant of keys is what lets it be tested with no
/// database at all.
/// </remarks>
/// <param name="Users">Generated timekeepers, in load order.</param>
/// <param name="Clients">Generated clients, in load order.</param>
/// <param name="Matters">Generated matters, each referencing a client by index.</param>
/// <param name="Entries">Generated time entries, referencing user and matter by index.</param>
public sealed record SeedDataSet(
    IReadOnlyList<User> Users,
    IReadOnlyList<Client> Clients,
    IReadOnlyList<MatterDraft> Matters,
    IReadOnlyList<TimeEntryDraft> Entries);

/// <summary>A matter before its client has a database key.</summary>
/// <param name="ClientIndex">Position of the owning client in <see cref="SeedDataSet.Clients"/>.</param>
/// <param name="MatterNumber">Reference unique within the owning client only.</param>
/// <param name="Name">Short description of the work.</param>
/// <param name="IsBillableByDefault">Default billable flag for entries on this matter.</param>
/// <param name="IsActive">Whether new time may be recorded against it.</param>
/// <param name="CreatedAtUtc">Creation timestamp, derived from the reference date.</param>
public sealed record MatterDraft(
    int ClientIndex,
    string MatterNumber,
    string Name,
    bool IsBillableByDefault,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>A time entry before its user and matter have database keys.</summary>
/// <param name="UserIndex">Position of the timekeeper in <see cref="SeedDataSet.Users"/>.</param>
/// <param name="MatterIndex">Position of the matter in <see cref="SeedDataSet.Matters"/>.</param>
/// <param name="WorkDate">The billing date.</param>
/// <param name="DurationMinutes">Duration, always a positive multiple of six, never above 1440.</param>
/// <param name="IsBillable">Whether the time is charged to the client.</param>
/// <param name="HourlyRateSnapshot">The timekeeper's rate captured at creation.</param>
/// <param name="Narrative">Description of the work.</param>
/// <param name="CreatedAtUtc">When the entry was recorded, near but not equal to the work date.</param>
public sealed record TimeEntryDraft(
    int UserIndex,
    int MatterIndex,
    DateOnly WorkDate,
    int DurationMinutes,
    bool IsBillable,
    decimal HourlyRateSnapshot,
    string Narrative,
    DateTime CreatedAtUtc);
