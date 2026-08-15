using LexTime.Domain.Rules;

namespace LexTime.Application.TimeEntries;

/// <summary>What a write use case did, or why it did not.</summary>
public enum WriteOutcome
{
    /// <summary>The entry was recorded or revised, and <see cref="TimeEntryWriteResult.Entry"/> carries it.</summary>
    Succeeded,

    /// <summary>
    /// One or more rules refused the submission. An ordinary outcome of a well-formed request,
    /// not a failure of the request's shape.
    /// </summary>
    RuleViolation,

    /// <summary>The entry named by the identifier does not exist.</summary>
    EntryNotFound,

    /// <summary>
    /// The timekeeper or matter named does not exist.
    /// </summary>
    /// <remarks>
    /// Deliberately distinct from a rule violation. An inactive matter <em>exists</em> and is
    /// refused by rule 5; a matter that does not exist is a different mistake and deserves a
    /// different answer — telling a caller their matter is "not active" when it was never there
    /// sends them to fix the wrong thing.
    /// </remarks>
    PartyNotFound,
}

/// <summary>The outcome of recording, revising or deleting an entry.</summary>
/// <param name="Outcome">Which of the four happened.</param>
/// <param name="Entry">The entry, when the outcome succeeded and there is one to return.</param>
/// <param name="Violations">
/// Every rule that refused the submission, not merely the first — a submission wrong in three
/// ways should not take three round trips to fix. Empty unless the outcome is
/// <see cref="WriteOutcome.RuleViolation"/>.
/// </param>
public sealed record TimeEntryWriteResult(
    WriteOutcome Outcome,
    TimeEntryDto? Entry,
    IReadOnlyList<RuleViolation> Violations)
{
    /// <summary>A successful write.</summary>
    /// <param name="entry">What was recorded or revised.</param>
    /// <returns>The result.</returns>
    public static TimeEntryWriteResult Success(TimeEntryDto entry) =>
        new(WriteOutcome.Succeeded, entry, []);

    /// <summary>A refusal by one or more rules.</summary>
    /// <param name="violations">Every rule that refused it.</param>
    /// <returns>The result.</returns>
    public static TimeEntryWriteResult Refused(IReadOnlyList<RuleViolation> violations) =>
        new(WriteOutcome.RuleViolation, null, violations);

    /// <summary>No entry with that identifier.</summary>
    /// <returns>The result.</returns>
    public static TimeEntryWriteResult EntryMissing() => new(WriteOutcome.EntryNotFound, null, []);

    /// <summary>No such timekeeper or matter.</summary>
    /// <returns>The result.</returns>
    public static TimeEntryWriteResult PartyMissing() => new(WriteOutcome.PartyNotFound, null, []);
}
