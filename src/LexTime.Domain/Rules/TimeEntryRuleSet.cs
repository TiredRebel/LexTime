namespace LexTime.Domain.Rules;

/// <summary>
/// The only place any time-entry rule is expressed.
/// </summary>
/// <remarks>
/// FR-011: the rules are enforced in one place and reached by every path that records or changes
/// an entry. <b>A rule restated in a handler or an endpoint is a defect even when it agrees with
/// this class</b> — because the two will eventually stop agreeing, and nothing will say so.
/// <para>
/// Pure by construction: no clock, no connection, no state. Everything it needs arrives in
/// <see cref="TimeEntryFacts"/>, which is what lets constitution P6 put the rules in the domain
/// while P4 keeps the domain free of persistence.
/// </para>
/// </remarks>
public static class TimeEntryRuleSet
{
    /// <summary>The billing increment, in minutes. A tenth of an hour.</summary>
    public const int IncrementMinutes = 6;

    /// <summary>The most minutes a single entry may record — one day.</summary>
    public const int MaximumEntryMinutes = 1440;

    /// <summary>The most minutes one timekeeper may record against one date.</summary>
    public const int MaximumDailyMinutes = 1440;

    /// <summary>How far back a work date may be submitted, in days.</summary>
    public const int BackdatingLimitDays = 90;

    /// <summary>
    /// Evaluates every applicable rule and returns each one that was broken.
    /// </summary>
    /// <remarks>
    /// Every violated rule is returned, not just the first: a submission wrong in three ways
    /// should not take three round trips to fix. The order is the order of the checks below and
    /// is stable, so a test can assert the whole collection rather than searching it.
    /// <para>
    /// An empty result is the accepting case. It is distinguishable from "not evaluated" because
    /// this method has no way to decline — given facts, it always answers.
    /// </para>
    /// </remarks>
    /// <param name="facts">Everything the rules need. See <see cref="TimeEntryFacts"/>.</param>
    /// <returns>The broken rules, empty when the submission conforms.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="facts"/> is null.</exception>
    public static IReadOnlyList<RuleViolation> Evaluate(TimeEntryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var violations = new List<RuleViolation>();

        // Rule 1. Zero and negatives land here rather than in a rule of their own: neither is a
        // positive multiple of six, and docs/prd.md §2.1 states this as one rule.
        if (facts.DurationMinutes <= 0 || facts.DurationMinutes % IncrementMinutes != 0)
        {
            violations.Add(new RuleViolation(
                DomainRule.DurationIncrement,
                facts.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Duration {facts.DurationMinutes} is not a positive multiple of {IncrementMinutes} minutes."));
        }

        // Rule 2. Checked independently of rule 1, so a duration that breaks both is reported as
        // breaking both. The first value that isolates this rule is 1446 — 1441 is refused by
        // rule 1 first and proves nothing about the maximum.
        if (facts.DurationMinutes > MaximumEntryMinutes)
        {
            violations.Add(new RuleViolation(
                DomainRule.DurationMaximum,
                facts.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Duration {facts.DurationMinutes} exceeds the {MaximumEntryMinutes}-minute maximum for a single entry."));
        }

        // Rule 3. OtherMinutesOnDate excludes the entry being revised, which is what makes a
        // duration reducible: an entry of 600 on a day totalling 1440 must be able to become
        // 300, and would not be if the day's total still counted the 600 being replaced.
        var dayTotal = facts.OtherMinutesOnDate + facts.DurationMinutes;
        if (dayTotal > MaximumDailyMinutes)
        {
            violations.Add(new RuleViolation(
                DomainRule.DailyMaximum,
                dayTotal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"This would bring the timekeeper's total for {facts.WorkDate:yyyy-MM-dd} to {dayTotal} " +
                $"minutes, above the {MaximumDailyMinutes}-minute daily maximum. " +
                $"{facts.OtherMinutesOnDate} minutes are already recorded."));
        }

        // Rule 4, when the work date is being submitted. Both ends inclusive: today is permitted,
        // and so is the ninetieth day back.
        if (facts.EvaluateWorkDate)
        {
            if (facts.WorkDate > facts.Today)
            {
                violations.Add(new RuleViolation(
                    DomainRule.BackdatingWindow,
                    facts.WorkDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    $"Work date {facts.WorkDate:yyyy-MM-dd} is in the future; today is {facts.Today:yyyy-MM-dd}."));
            }
            else if (facts.WorkDate < facts.Today.AddDays(-BackdatingLimitDays))
            {
                violations.Add(new RuleViolation(
                    DomainRule.BackdatingWindow,
                    facts.WorkDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    $"Work date {facts.WorkDate:yyyy-MM-dd} is more than {BackdatingLimitDays} days before " +
                    $"today ({facts.Today:yyyy-MM-dd}); the earliest permitted date is " +
                    $"{facts.Today.AddDays(-BackdatingLimitDays):yyyy-MM-dd}."));
            }
        }

        // Rule 5, when the matter is being submitted. The two flags are reported separately
        // because a caller told only "not active" cannot tell which one to reopen.
        if (facts.EvaluateMatter)
        {
            if (!facts.MatterIsActive)
            {
                violations.Add(new RuleViolation(
                    DomainRule.ActiveMatterAndClient,
                    "matter",
                    "The matter is not active. Time may only be recorded against an active matter " +
                    "of an active client."));
            }
            else if (!facts.ClientIsActive)
            {
                violations.Add(new RuleViolation(
                    DomainRule.ActiveMatterAndClient,
                    "client",
                    "The matter is active but its client is not. Time may only be recorded against " +
                    "an active matter of an active client."));
            }
        }

        // FR-013. Not one of the six, and flagged as an addition in the specification.
        if (!facts.TimekeeperIsActive)
        {
            violations.Add(new RuleViolation(
                DomainRule.ActiveTimekeeper,
                "timekeeper",
                "The timekeeper is not active and may not record time."));
        }

        return violations;
    }
}
