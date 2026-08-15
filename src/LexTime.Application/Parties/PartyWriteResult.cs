namespace LexTime.Application.Parties;

/// <summary>Outcome of a client or matter write.</summary>
public enum PartyWriteOutcome
{
    /// <summary>The record was created or revised.</summary>
    Succeeded,

    /// <summary>A database uniqueness rule rejected the write.</summary>
    Conflict,

    /// <summary>The target or required parent does not exist.</summary>
    NotFound,
}

/// <summary>Which field caused a translated uniqueness conflict.</summary>
/// <param name="Field">API field name.</param>
/// <param name="Value">Value that collided.</param>
public sealed record PartyConflict(string Field, string Value);

/// <summary>Application-visible form of a database uniqueness refusal.</summary>
public sealed class PartyConstraintConflictException : Exception
{
    /// <summary>Creates a translated uniqueness exception.</summary>
    /// <param name="conflict">Field and submitted value that collided.</param>
    /// <param name="innerException">Original database exception.</param>
    public PartyConstraintConflictException(PartyConflict conflict, Exception innerException)
        : base(BuildMessage(conflict), innerException)
    {
        Conflict = conflict;
    }

    /// <summary>Field and value that collided.</summary>
    public PartyConflict Conflict { get; }

    /// <summary>Builds the exception message after validating the conflict.</summary>
    /// <param name="conflict">Conflict to describe.</param>
    /// <returns>A stable diagnostic message.</returns>
    private static string BuildMessage(PartyConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return $"The value for '{conflict.Field}' is already in use.";
    }
}

/// <summary>Result shared by client and matter write use cases.</summary>
/// <param name="Outcome">Write outcome.</param>
/// <param name="Client">Client record when the write concerns a client.</param>
/// <param name="Matter">Matter record when the write concerns a matter.</param>
/// <param name="Conflict">Translated uniqueness conflict, when present.</param>
public sealed record PartyWriteResult(
    PartyWriteOutcome Outcome,
    ClientDto? Client,
    MatterDto? Matter,
    PartyConflict? Conflict)
{
    /// <summary>Creates a successful client result.</summary>
    /// <param name="client">Created or revised client.</param>
    /// <returns>The result.</returns>
    public static PartyWriteResult ClientSucceeded(ClientDto client) => new(PartyWriteOutcome.Succeeded, client, null, null);

    /// <summary>Creates a successful matter result.</summary>
    /// <param name="matter">Created or revised matter.</param>
    /// <returns>The result.</returns>
    public static PartyWriteResult MatterSucceeded(MatterDto matter) => new(PartyWriteOutcome.Succeeded, null, matter, null);

    /// <summary>Creates a conflict result.</summary>
    /// <param name="conflict">Translated conflict.</param>
    /// <returns>The result.</returns>
    public static PartyWriteResult Conflicted(PartyConflict conflict) => new(PartyWriteOutcome.Conflict, null, null, conflict);

    /// <summary>Creates a not-found result.</summary>
    /// <returns>The result.</returns>
    public static PartyWriteResult Missing() => new(PartyWriteOutcome.NotFound, null, null, null);
}
