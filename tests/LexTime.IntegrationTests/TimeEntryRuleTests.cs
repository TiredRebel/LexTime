using LexTime.Domain.Rules;

namespace LexTime.IntegrationTests;

/// <summary>
/// The six rules of <c>docs/prd.md</c> §2.1, each refusing and each accepting.
/// </summary>
/// <remarks>
/// No container, no database, no clock. The rules are a pure function of
/// <see cref="TimeEntryFacts"/>, so every case runs in microseconds — which is the point.
/// Exhaustiveness is affordable here, and a rule suite that needed a container per case would be
/// slow enough that nobody would grow it.
/// <para>
/// These ask <em>is the rule right</em>. <see cref="TimeEntryWriteTests"/> asks <em>is the rule
/// reached</em>, and both are needed: a feature enforcing all six perfectly in a class nothing
/// called would pass this file completely.
/// </para>
/// <para>
/// Every date is computed relative to <see cref="Today"/>. Not one is a literal — a test
/// asserting some fixed date sits inside the 90-day window passes now and fails in three months,
/// and a suite that rots on a date fails while nothing is wrong (FR-026, SC-009).
/// </para>
/// </remarks>
public sealed class TimeEntryRuleTests
{
    /// <summary>The date these tests treat as today. Arbitrary, fixed, and far from any boundary.</summary>
    private static readonly DateOnly Today = FixedClock.Default.Today;

    /// <summary>A submission that breaks nothing, which each test perturbs in exactly one way.</summary>
    /// <remarks>
    /// Starting from a conforming baseline is what makes a refusal attributable: if the test
    /// changes one field and one rule fires, that rule fired because of that field.
    /// </remarks>
    private static TimeEntryFacts Valid => new(
        DurationMinutes: 60,
        WorkDate: Today.AddDays(-1),
        Today: Today,
        OtherMinutesOnDate: 0,
        MatterIsActive: true,
        ClientIsActive: true,
        TimekeeperIsActive: true);

    // ---- Rule 1: a positive multiple of six minutes ----

    /// <summary>Durations that are not positive multiples of six are refused.</summary>
    /// <param name="minutes">The duration to submit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    [InlineData(7)]
    [InlineData(1)]
    [InlineData(59)]
    public void RefusesADurationThatIsNotAPositiveMultipleOfSix(int minutes) =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { DurationMinutes = minutes }),
            v => v.Rule == DomainRule.DurationIncrement);

    /// <summary>Durations that are positive multiples of six are accepted.</summary>
    /// <param name="minutes">The duration to submit.</param>
    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(600)]
    [InlineData(1440)]
    public void AcceptsADurationThatIsAPositiveMultipleOfSix(int minutes) =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(Valid with { DurationMinutes = minutes }));

    // ---- Rule 2: at most 24 hours in one entry ----

    /// <summary>A duration above the single-entry maximum is refused.</summary>
    /// <remarks>
    /// <b>1446, not 1441.</b> 1441 is refused by rule 1 for not being a multiple of six, so it
    /// proves nothing about this rule — the first value that isolates the maximum is the next
    /// legal increment above it.
    /// </remarks>
    [Fact]
    public void RefusesASingleEntryAboveTheMaximum() =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { DurationMinutes = 1446 }),
            v => v.Rule == DomainRule.DurationMaximum);

    /// <summary>A duration exactly at the maximum is accepted. The boundary is a value, not a region.</summary>
    [Fact]
    public void AcceptsASingleEntryExactlyAtTheMaximum() =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(Valid with { DurationMinutes = 1440 }));

    // ---- Rule 3: at most 24 hours per timekeeper per date ----

    /// <summary>A submission that would push the day's total above the maximum is refused.</summary>
    /// <remarks>
    /// Every figure here is a legal duration in its own right — 1398 is 233 increments and 60 is
    /// ten. That matters: a test using 1,400 and 40 would look reasonable and prove nothing,
    /// because neither is a multiple of six and rule 1 would refuse the submission before rule 3
    /// was ever reached. The same trap as testing rule 2 with 1441.
    /// </remarks>
    [Fact]
    public void RefusesASubmissionThatWouldExceedTheDailyMaximum() =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { OtherMinutesOnDate = 1398, DurationMinutes = 60 }),
            v => v.Rule == DomainRule.DailyMaximum);

    /// <summary>A submission that fits inside the remaining minutes is accepted.</summary>
    [Fact]
    public void AcceptsASubmissionThatFitsWithinTheDailyMaximum() =>
        Assert.Empty(
            TimeEntryRuleSet.Evaluate(Valid with { OtherMinutesOnDate = 1398, DurationMinutes = 42 }));

    /// <summary>A day's total of exactly the maximum is accepted.</summary>
    [Fact]
    public void AcceptsADayTotallingExactlyTheMaximum() =>
        Assert.Empty(
            TimeEntryRuleSet.Evaluate(Valid with { OtherMinutesOnDate = 1380, DurationMinutes = 60 }));

    /// <summary>
    /// An entry on a full day can still be reduced.
    /// </summary>
    /// <remarks>
    /// The case that catches an implementation counting an entry against itself. A day totalling
    /// 1440 including this entry's 600 minutes has 840 minutes recorded by <em>other</em>
    /// entries, so replacing 600 with 300 is legitimate and must be accepted. An implementation
    /// that passed the whole day's total here would refuse it — and no test of an <em>increase</em>
    /// would ever notice.
    /// </remarks>
    [Fact]
    public void AcceptsAReductionOnADayAlreadyAtTheMaximum() =>
        Assert.Empty(
            TimeEntryRuleSet.Evaluate(Valid with { OtherMinutesOnDate = 840, DurationMinutes = 300 }));

    // ---- Rule 4: the backdating window ----

    /// <summary>A work date in the future is refused.</summary>
    [Fact]
    public void RefusesAWorkDateInTheFuture() =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { WorkDate = Today.AddDays(1) }),
            v => v.Rule == DomainRule.BackdatingWindow);

    /// <summary>A work date beyond the backdating limit is refused.</summary>
    [Fact]
    public void RefusesAWorkDateBeyondTheBackdatingLimit() =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { WorkDate = Today.AddDays(-91) }),
            v => v.Rule == DomainRule.BackdatingWindow);

    /// <summary>Today and the oldest permitted date are both accepted — the window is inclusive.</summary>
    /// <param name="daysBack">How far back to date the entry.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(90)]
    public void AcceptsAWorkDateInsideTheBackdatingWindow(int daysBack) =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(Valid with { WorkDate = Today.AddDays(-daysBack) }));

    /// <summary>
    /// An out-of-window date is not evaluated when the work date is not being changed.
    /// </summary>
    /// <remarks>
    /// The clarification made testable: an update that leaves a field alone is not a submission
    /// of that field, so an entry recorded 200 days ago can still have its narrative corrected.
    /// </remarks>
    [Fact]
    public void SkipsTheBackdatingWindowWhenTheWorkDateIsNotBeingChanged() =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(
            Valid with { WorkDate = Today.AddDays(-200), EvaluateWorkDate = false }));

    // ---- Rule 5: an active matter of an active client ----

    /// <summary>An inactive matter is refused, and the refusal names the matter.</summary>
    [Fact]
    public void RefusesAnInactiveMatterAndSaysSo()
    {
        var violation = Assert.Single(TimeEntryRuleSet.Evaluate(Valid with { MatterIsActive = false }));

        Assert.Equal(DomainRule.ActiveMatterAndClient, violation.Rule);
        Assert.Equal("matter", violation.OffendingValue);
    }

    /// <summary>
    /// An active matter of an inactive client is refused, and the refusal names the client.
    /// </summary>
    /// <remarks>
    /// The two are reported separately because a caller told only "not active" cannot tell
    /// whether to reopen a matter or a client (FR-008). This assertion is the one that would fail
    /// if the two flags were ever collapsed into one.
    /// </remarks>
    [Fact]
    public void RefusesAnInactiveClientAndSaysSo()
    {
        var violation = Assert.Single(TimeEntryRuleSet.Evaluate(Valid with { ClientIsActive = false }));

        Assert.Equal(DomainRule.ActiveMatterAndClient, violation.Rule);
        Assert.Equal("client", violation.OffendingValue);
    }

    /// <summary>An active matter of an active client is accepted.</summary>
    [Fact]
    public void AcceptsAnActiveMatterOfAnActiveClient() =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(Valid with { MatterIsActive = true, ClientIsActive = true }));

    /// <summary>An inactive matter is not evaluated when the matter is not being changed.</summary>
    [Fact]
    public void SkipsTheActiveMatterRuleWhenTheMatterIsNotBeingChanged() =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(
            Valid with { MatterIsActive = false, ClientIsActive = false, EvaluateMatter = false }));

    // ---- FR-013: an active timekeeper ----

    /// <summary>An inactive timekeeper may not record time.</summary>
    [Fact]
    public void RefusesAnInactiveTimekeeper() =>
        Assert.Contains(
            TimeEntryRuleSet.Evaluate(Valid with { TimekeeperIsActive = false }),
            v => v.Rule == DomainRule.ActiveTimekeeper);

    /// <summary>An active timekeeper may.</summary>
    [Fact]
    public void AcceptsAnActiveTimekeeper() =>
        Assert.Empty(TimeEntryRuleSet.Evaluate(Valid with { TimekeeperIsActive = true }));

    // ---- Reporting behaviour ----

    /// <summary>
    /// A submission breaking several rules reports all of them.
    /// </summary>
    /// <remarks>
    /// A caller should be able to fix everything in one pass. Reporting only the first violation
    /// turns one bad submission into three round trips.
    /// </remarks>
    [Fact]
    public void ReportsEveryBrokenRuleRatherThanTheFirst()
    {
        var violations = TimeEntryRuleSet.Evaluate(Valid with
        {
            DurationMinutes = 7,
            WorkDate = Today.AddDays(5),
            MatterIsActive = false,
        });

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Rule == DomainRule.DurationIncrement);
        Assert.Contains(violations, v => v.Rule == DomainRule.BackdatingWindow);
        Assert.Contains(violations, v => v.Rule == DomainRule.ActiveMatterAndClient);
    }

    /// <summary>
    /// Every refusal names the value that broke the rule.
    /// </summary>
    /// <remarks>
    /// SC-004 is judged by someone who has not read the code and must be able to say what to
    /// change. A message naming the rule but not the value leaves them guessing which field was
    /// wrong, and "invalid request" is explicitly not compliance (FR-010).
    /// </remarks>
    [Fact]
    public void NamesTheOffendingValueInEveryRefusal()
    {
        var violations = TimeEntryRuleSet.Evaluate(Valid with
        {
            DurationMinutes = 1446,
            WorkDate = Today.AddDays(-500),
        });

        Assert.NotEmpty(violations);
        Assert.All(violations, v => Assert.False(string.IsNullOrWhiteSpace(v.OffendingValue)));
        Assert.All(violations, v => Assert.False(string.IsNullOrWhiteSpace(v.Detail)));

        // The detail has to carry the limit as well as the value: "duration 1446 is invalid"
        // tells a caller nothing they can act on.
        Assert.Contains(violations, v => v.Detail.Contains("1440", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Detail.Contains("90 days", StringComparison.Ordinal));
    }
}
