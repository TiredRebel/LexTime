using LexTime.Domain.Entities;

namespace LexTime.Application.Parties;

/// <summary>Client data returned by the API.</summary>
/// <param name="ClientId">Database identifier.</param>
/// <param name="ClientCode">Immutable firm-wide reference.</param>
/// <param name="Name">Current client name.</param>
/// <param name="IsActive">Whether new time may be recorded for the client.</param>
/// <param name="CreatedAtUtc">When the client was created.</param>
public sealed record ClientDto(int ClientId, string ClientCode, string Name, bool IsActive, DateTime CreatedAtUtc);

/// <summary>Matter data returned by the API.</summary>
/// <param name="MatterId">Database identifier.</param>
/// <param name="ClientId">Owning client identifier.</param>
/// <param name="MatterNumber">Immutable number within the client.</param>
/// <param name="Name">Current matter name.</param>
/// <param name="IsBillableByDefault">Default billable flag for new entries.</param>
/// <param name="IsActive">Whether new time may be recorded for the matter.</param>
/// <param name="CreatedAtUtc">When the matter was created.</param>
public sealed record MatterDto(
    int MatterId,
    int ClientId,
    string MatterNumber,
    string Name,
    bool IsBillableByDefault,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Timekeeper data returned by the API.</summary>
/// <param name="UserId">Database identifier.</param>
/// <param name="Email">Timekeeper email address.</param>
/// <param name="FullName">Display name.</param>
/// <param name="DefaultHourlyRate">Current rate used for newly recorded entries.</param>
/// <param name="IsActive">Whether new time may be recorded for the timekeeper.</param>
public sealed record TimekeeperDto(
    int UserId,
    string Email,
    string FullName,
    decimal DefaultHourlyRate,
    bool IsActive);

/// <summary>A bounded page of clients.</summary>
/// <param name="Skip">Effective offset.</param>
/// <param name="Take">Effective page size.</param>
/// <param name="Total">Total matching clients.</param>
/// <param name="Items">Clients in identifier order.</param>
public sealed record ClientPage(int Skip, int Take, int Total, IReadOnlyList<ClientDto> Items);

/// <summary>A bounded page of matters.</summary>
/// <param name="Skip">Effective offset.</param>
/// <param name="Take">Effective page size.</param>
/// <param name="Total">Total matching matters.</param>
/// <param name="Items">Matters in identifier order.</param>
public sealed record MatterPage(int Skip, int Take, int Total, IReadOnlyList<MatterDto> Items);

/// <summary>A bounded page of timekeepers.</summary>
/// <param name="Skip">Effective offset.</param>
/// <param name="Take">Effective page size.</param>
/// <param name="Total">Total matching timekeepers.</param>
/// <param name="Items">Timekeepers in identifier order.</param>
public sealed record TimekeeperPage(int Skip, int Take, int Total, IReadOnlyList<TimekeeperDto> Items);

/// <summary>Entity-to-API projections for party records.</summary>
public static class PartyDtoExtensions
{
    /// <summary>Projects a client.</summary>
    /// <param name="client">Client to project.</param>
    /// <returns>The client DTO.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public static ClientDto ToDto(this Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new(client.ClientId, client.ClientCode, client.Name, client.IsActive, client.CreatedAtUtc);
    }

    /// <summary>Projects a matter.</summary>
    /// <param name="matter">Matter to project.</param>
    /// <returns>The matter DTO.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="matter"/> is null.</exception>
    public static MatterDto ToDto(this Matter matter)
    {
        ArgumentNullException.ThrowIfNull(matter);
        return new(
            matter.MatterId,
            matter.ClientId,
            matter.MatterNumber,
            matter.Name,
            matter.IsBillableByDefault,
            matter.IsActive,
            matter.CreatedAtUtc);
    }

    /// <summary>Projects a timekeeper.</summary>
    /// <param name="user">Timekeeper to project.</param>
    /// <returns>The timekeeper DTO.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is null.</exception>
    public static TimekeeperDto ToDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.UserId, user.Email, user.FullName, user.DefaultHourlyRate, user.IsActive);
    }
}
