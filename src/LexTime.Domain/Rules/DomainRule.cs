namespace LexTime.Domain.Rules;

/// <summary>
/// The rules a time entry must satisfy, as <c>docs/prd.md</c> §2.1 states them.
/// </summary>
/// <remarks>
/// Named and enumerated so a refusal can say which rule it was without a caller parsing English,
/// and so a client can branch on the answer. The numbering in the summaries matches the PRD's
/// list, which is the document a reviewer will check this against.
/// </remarks>
public enum DomainRule
{
    /// <summary>
    /// Rule 1 — duration is a positive whole number of minutes and a multiple of six.
    /// </summary>
    /// <remarks>
    /// Six minutes is a tenth of an hour, which is how legal billing is quoted. An entry that
    /// cannot be expressed in tenths cannot be invoiced. Zero and negative durations fail here
    /// rather than needing a rule of their own — they are not positive multiples of six, and a
    /// separate rule would be two rules where the PRD has one.
    /// </remarks>
    DurationIncrement,

    /// <summary>Rule 2 — a single entry may not exceed 1440 minutes, the length of a day.</summary>
    DurationMaximum,

    /// <summary>
    /// Rule 3 — one timekeeper's total for one work date may not exceed 1440 minutes.
    /// </summary>
    /// <remarks>
    /// Counts every entry for that timekeeper and date, billable or not: the billable flag
    /// decides what is charged, not what is possible.
    /// </remarks>
    DailyMaximum,

    /// <summary>
    /// Rule 4 — the work date may not be in the future, nor more than 90 days in the past.
    /// </summary>
    /// <remarks>
    /// A stand-in for a period-close rule. It governs what may be <em>submitted</em>, not what
    /// may exist: seeded history spans 24 months and is not invalidated by it. Both ends are
    /// inclusive — today is permitted, and so is the ninetieth day back.
    /// </remarks>
    BackdatingWindow,

    /// <summary>Rule 5 — the matter must be active and belong to an active client.</summary>
    /// <remarks>
    /// A refusal must say which of the two failed. A caller told only "not active" cannot tell
    /// whether to reopen a matter or a client, and a message a caller cannot act on has failed
    /// at its only job.
    /// </remarks>
    ActiveMatterAndClient,

    /// <summary>
    /// Rule 6 — the timekeeper's rate is captured when the entry is recorded and never rewritten.
    /// </summary>
    /// <remarks>
    /// <b>This rule cannot be violated by a submission</b>, so it never appears as a violation.
    /// It is a statement about what the system does rather than about what a caller may send, and
    /// it is enforced structurally: the revise command has no rate field, so the API offers no
    /// way to change one. Present in this enumeration for completeness, because a rule that is
    /// absent from the list of rules is a rule someone will forget.
    /// </remarks>
    RateSnapshot,

    /// <summary>
    /// An entry may not be recorded against an inactive timekeeper.
    /// </summary>
    /// <remarks>
    /// Not one of the six <c>docs/prd.md</c> §2.1 lists. Added as FR-013 on the same reasoning as
    /// <see cref="ActiveMatterAndClient"/>, and flagged as an addition in the specification's
    /// checklist rather than slipped in: without it, someone who has left the firm can keep
    /// logging time.
    /// </remarks>
    ActiveTimekeeper,
}
