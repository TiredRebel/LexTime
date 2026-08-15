namespace LexTime.Application.Parties;

/// <summary>Creates an active client; its code is immutable after creation.</summary>
/// <param name="ClientCode">Firm-wide client reference.</param>
/// <param name="Name">Client name.</param>
public sealed record RegisterClientCommand(string ClientCode, string Name);

/// <summary>Changes mutable client fields.</summary>
/// <param name="Name">Replacement client name.</param>
/// <param name="IsActive">Requested active state.</param>
public sealed record ReviseClientCommand(string Name, bool IsActive);

/// <summary>Creates an active matter under the client in the route.</summary>
/// <param name="MatterNumber">Matter reference within its client.</param>
/// <param name="Name">Matter name.</param>
/// <param name="IsBillableByDefault">Default billable flag for new entries.</param>
public sealed record OpenMatterCommand(string MatterNumber, string Name, bool IsBillableByDefault);

/// <summary>Changes mutable matter fields.</summary>
/// <param name="Name">Replacement matter name.</param>
/// <param name="IsBillableByDefault">Replacement default billable flag.</param>
/// <param name="IsActive">Requested active state.</param>
public sealed record ReviseMatterCommand(string Name, bool IsBillableByDefault, bool IsActive);

/// <summary>Filters and pages clients.</summary>
/// <param name="IsActive">Optional active-state filter.</param>
/// <param name="Skip">Requested offset.</param>
/// <param name="Take">Requested page size.</param>
public sealed record ListClientsQuery(bool? IsActive, int Skip, int Take)
{
    /// <summary>Default page size.</summary>
    public const int DefaultTake = 50;

    /// <summary>Maximum page size.</summary>
    public const int MaximumTake = 200;

    /// <summary>Clamps the page window.</summary>
    /// <returns>A query with a valid page window.</returns>
    public ListClientsQuery Clamped() => this with
    {
        Skip = Math.Max(0, Skip),
        Take = Take <= 0 ? DefaultTake : Math.Min(Take, MaximumTake),
    };
}

/// <summary>Filters and pages matters belonging to one client.</summary>
/// <param name="ClientId">Owning client.</param>
/// <param name="Skip">Requested offset.</param>
/// <param name="Take">Requested page size.</param>
public sealed record ListMattersQuery(int ClientId, int Skip, int Take)
{
    /// <summary>Default page size.</summary>
    public const int DefaultTake = 50;

    /// <summary>Maximum page size.</summary>
    public const int MaximumTake = 200;

    /// <summary>Clamps the page window.</summary>
    /// <returns>A query with a valid page window.</returns>
    public ListMattersQuery Clamped() => this with
    {
        Skip = Math.Max(0, Skip),
        Take = Take <= 0 ? DefaultTake : Math.Min(Take, MaximumTake),
    };
}

/// <summary>Pages all seeded timekeepers.</summary>
/// <param name="Skip">Requested offset.</param>
/// <param name="Take">Requested page size.</param>
public sealed record ListTimekeepersQuery(int Skip, int Take)
{
    /// <summary>Default page size.</summary>
    public const int DefaultTake = 50;

    /// <summary>Maximum page size.</summary>
    public const int MaximumTake = 200;

    /// <summary>Clamps the page window.</summary>
    /// <returns>A query with a valid page window.</returns>
    public ListTimekeepersQuery Clamped() => this with
    {
        Skip = Math.Max(0, Skip),
        Take = Take <= 0 ? DefaultTake : Math.Min(Take, MaximumTake),
    };
}
