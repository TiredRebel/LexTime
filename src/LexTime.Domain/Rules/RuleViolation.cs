namespace LexTime.Domain.Rules;

/// <summary>
/// One rule, broken, with enough detail for the caller to act.
/// </summary>
/// <remarks>
/// <b>Returned, never thrown.</b> A refused submission is an ordinary outcome of a well-formed
/// request, not an exceptional condition. Throwing would make a handler's signature lie about
/// what it does and would push rule handling into middleware, where a reader cannot see which
/// rules exist.
/// <para>
/// FR-010 forbids "invalid request" as compliance, and SC-004 is judged by someone who has not
/// read the code and must be able to say what to change. Both halves are needed: the sentence
/// for a human, the rule and the value for a client that wants to highlight the offending field.
/// </para>
/// </remarks>
/// <param name="Rule">Which rule was broken.</param>
/// <param name="OffendingValue">
/// The value that broke it, rendered for display. A message naming the rule but not the value
/// leaves the caller guessing which of their fields was wrong.
/// </param>
/// <param name="Detail">
/// One sentence naming both the offending value and the limit it exceeded. The limit matters as
/// much as the value: "duration 7 is invalid" tells a caller nothing they can fix.
/// </param>
public sealed record RuleViolation(DomainRule Rule, string OffendingValue, string Detail);
