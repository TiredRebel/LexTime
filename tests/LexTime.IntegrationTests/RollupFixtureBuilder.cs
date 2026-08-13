using LexTime.Infrastructure.Persistence;

namespace LexTime.IntegrationTests;

/// <summary>
/// Builds the small, deliberately-shaped datasets the rollup's arithmetic is checked against.
/// </summary>
/// <remarks>
/// Small enough that every expected figure was worked out on paper and written into
/// <see cref="WeeklyBillableRollupTests"/> as a literal before the procedure was ever run
/// against it (constitution P12). Window-function errors are self-consistent — a wrong running
/// total is wrong in the same direction on every row, so nothing looks anomalous — and an
/// expectation derived from the procedure's own output would agree with any of them.
/// <para>
/// Every client below exists to make one rule fail if it is broken. The roles are documented
/// per client because the expectations in the test file are unreadable without them.
/// </para>
/// </remarks>
internal static class RollupFixtureBuilder
{
    /// <summary>Rate on <c>BIG</c>'s entries. Different from everyone else's on purpose.</summary>
    /// <remarks>
    /// Two rates in the fixture is what makes the amount column meaningful: with a single rate
    /// throughout, an implementation that multiplied by a constant would pass.
    /// </remarks>
    public const decimal BigRate = 500.00m;

    /// <summary>Rate on every other client's entries.</summary>
    public const decimal StandardRate = 400.00m;

    /// <summary>
    /// First day of the reported range: a Monday, so nothing precedes it that the report may
    /// see, and every row in the first week must report an absent change.
    /// </summary>
    public static readonly DateOnly RangeStart = new(2026, 1, 5);

    /// <summary>
    /// Last day of the reported range: a <em>Wednesday</em>, deliberately mid-week.
    /// </summary>
    /// <remarks>
    /// The final week is therefore partial, and <c>BIG</c> has an entry on the Thursday
    /// immediately after that must not appear in any figure. A range that silently widened to
    /// whole weeks would swallow it and nothing else in the fixture would notice.
    /// </remarks>
    public static readonly DateOnly RangeEnd = new(2026, 2, 4);

    /// <summary>Monday of the first reported week (ISO 2026-W02).</summary>
    public static readonly DateOnly Week1 = new(2026, 1, 5);

    /// <summary>Monday of the second reported week (ISO 2026-W03).</summary>
    public static readonly DateOnly Week2 = new(2026, 1, 12);

    /// <summary>Monday of the third reported week (ISO 2026-W04).</summary>
    public static readonly DateOnly Week3 = new(2026, 1, 19);

    /// <summary>Monday of the fourth reported week (ISO 2026-W05).</summary>
    public static readonly DateOnly Week4 = new(2026, 1, 26);

    /// <summary>Monday of the fifth, partial reported week (ISO 2026-W06).</summary>
    public static readonly DateOnly Week5 = new(2026, 2, 2);

    /// <summary>First day of the year-boundary fixture's range: Monday of ISO 2025-W52.</summary>
    public static readonly DateOnly BoundaryRangeStart = new(2025, 12, 22);

    /// <summary>Last day of the year-boundary fixture's range.</summary>
    public static readonly DateOnly BoundaryRangeEnd = new(2026, 1, 18);

    /// <summary>Monday of ISO 2025 week 52 — the week before the turn.</summary>
    public static readonly DateOnly BoundaryWeek52 = new(2025, 12, 22);

    /// <summary>
    /// Monday of ISO <em>2026</em> week 1, whose days are mostly in December 2025.
    /// </summary>
    /// <remarks>
    /// This date is the entire reason the year-boundary test exists. Its week ordinal is one
    /// greater than <see cref="BoundaryWeek52"/>'s, so "the preceding week" is found correctly
    /// by date arithmetic — while its ISO week number is 1, so an implementation deriving the
    /// preceding week from the number would look for week 0 and find nothing.
    /// </remarks>
    public static readonly DateOnly BoundaryWeek1 = new(2025, 12, 29);

    /// <summary>Monday of ISO 2026 week 3, after a silent week 2.</summary>
    public static readonly DateOnly BoundaryWeek3 = new(2026, 1, 12);

    /// <summary>
    /// Populates the main fixture: five weeks, five clients, one timekeeper.
    /// </summary>
    /// <remarks>
    /// Client roles, each chosen to break one rule if that rule is implemented wrongly:
    /// <list type="bullet">
    /// <item><description>
    /// <c>BIG</c> — active in all five weeks at a distinct rate. Supplies the ordinary
    /// week-on-week comparisons, the top of most weeks' standings, and the out-of-range
    /// Thursday entry that the partial final week must exclude.
    /// </description></item>
    /// <item><description>
    /// <c>GAP</c> — bills in week 1, silent through weeks 2 and 3, bills again in week 4.
    /// <b>Its week-1 total (8h) and its week-4 total (3h) differ and neither is zero</b>, so
    /// the correct answer (3h, against a silent week 3) and the wrong one (-5h, against the
    /// week it last billed in) cannot be confused. A fixture where those coincided would pass
    /// under both implementations and prove nothing.
    /// </description></item>
    /// <item><description>
    /// <c>LATE</c> — first appears in week 3. Its change must be a number, not absent: weeks 1
    /// and 2 are inside the range and it was simply silent. This separates "the client's first
    /// row" from "the edge of the range", which are the two things the absent case is easily
    /// confused between.
    /// </description></item>
    /// <item><description>
    /// <c>TIED</c> — exactly equals <c>BIG</c>'s week-2 billable hours, so the two share a
    /// standing and the next client must take the position immediately after, not the one
    /// after that.
    /// </description></item>
    /// <item><description>
    /// <c>QUIET</c> — logs only non-billable time in week 2, then a little billable time in
    /// week 3. Proves a week of unbilled work still produces a row, and gives the endpoint's
    /// filter test a client that is not top of its weeks.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="context">Context supplying the connection. Must be an isolated database.</param>
    /// <returns>The generated client identifiers.</returns>
    public static async Task<RollupFixture> PopulateAsync(LexTimeDbContext context)
    {
        var userId = await DirectSql.InsertUserAsync(context, "rollup@lextime.test")
            .ConfigureAwait(false);

        var big = await AddClientAsync(context, "BIG").ConfigureAwait(false);
        var gap = await AddClientAsync(context, "GAP").ConfigureAwait(false);
        var late = await AddClientAsync(context, "LATE").ConfigureAwait(false);
        var tied = await AddClientAsync(context, "TIED").ConfigureAwait(false);
        var quiet = await AddClientAsync(context, "QUIET").ConfigureAwait(false);

        // BIG: 10h, 12h, 8h, 5h, 6h across the five weeks, at 500/hour.
        await EntryAsync(context, userId, big.MatterId, Week1.AddDays(1), 600, true, BigRate).ConfigureAwait(false);
        await EntryAsync(context, userId, big.MatterId, Week2.AddDays(1), 720, true, BigRate).ConfigureAwait(false);
        await EntryAsync(context, userId, big.MatterId, Week3.AddDays(1), 480, true, BigRate).ConfigureAwait(false);
        await EntryAsync(context, userId, big.MatterId, Week4.AddDays(1), 300, true, BigRate).ConfigureAwait(false);
        await EntryAsync(context, userId, big.MatterId, Week5.AddDays(1), 360, true, BigRate).ConfigureAwait(false);

        // Thursday of week 5, one day past RangeEnd. Large enough that including it would
        // distort BIG's final week, its running total and its standing all at once.
        await EntryAsync(context, userId, big.MatterId, Week5.AddDays(3), 900, true, BigRate).ConfigureAwait(false);

        // GAP: 8h in week 1, nothing in weeks 2 and 3, 3h in week 4.
        await EntryAsync(context, userId, gap.MatterId, Week1.AddDays(2), 480, true, StandardRate).ConfigureAwait(false);
        await EntryAsync(context, userId, gap.MatterId, Week4.AddDays(2), 180, true, StandardRate).ConfigureAwait(false);

        // LATE: nothing until week 3, then 4h and 6h.
        await EntryAsync(context, userId, late.MatterId, Week3.AddDays(2), 240, true, StandardRate).ConfigureAwait(false);
        await EntryAsync(context, userId, late.MatterId, Week4.AddDays(3), 360, true, StandardRate).ConfigureAwait(false);

        // TIED: 12h in week 2 — the same billable total as BIG that week.
        await EntryAsync(context, userId, tied.MatterId, Week2.AddDays(2), 720, true, StandardRate).ConfigureAwait(false);

        // QUIET: 5h non-billable in week 2, then 2h billable in week 3.
        await EntryAsync(context, userId, quiet.MatterId, Week2.AddDays(3), 300, false, StandardRate).ConfigureAwait(false);
        await EntryAsync(context, userId, quiet.MatterId, Week3.AddDays(3), 120, true, StandardRate).ConfigureAwait(false);

        return new RollupFixture(big.ClientId, gap.ClientId, late.ClientId, tied.ClientId, quiet.ClientId);
    }

    /// <summary>
    /// Populates the year-boundary fixture: one client across the turn of the year.
    /// </summary>
    /// <remarks>
    /// Three weeks with a silent one between the last two:
    /// <list type="bullet">
    /// <item><description>ISO 2025 week 52, beginning 2025-12-22 — 8 billable hours.</description></item>
    /// <item><description>
    /// ISO <b>2026</b> week 1, beginning 2025-12-29 — 5 billable hours. Its change must be
    /// <c>-3</c>, measured against the week before it. An implementation deriving the preceding
    /// week from the ISO week number would look for week 0, find nothing, treat the week as
    /// silent, and report <c>+5</c> instead.
    /// </description></item>
    /// <item><description>
    /// ISO 2026 week 3, beginning 2026-01-12 — 6 billable hours, after a silent week 2. Its
    /// change must be <c>+6</c>. Together with the row above this pins both branches on the
    /// same client, so neither can be satisfied by hard-coding the other.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="context">Context supplying the connection. Must be an isolated database.</param>
    /// <returns>The identifier of the single client created.</returns>
    public static async Task<int> PopulateYearBoundaryAsync(LexTimeDbContext context)
    {
        var userId = await DirectSql.InsertUserAsync(context, "boundary@lextime.test")
            .ConfigureAwait(false);

        var client = await AddClientAsync(context, "TURN").ConfigureAwait(false);

        await EntryAsync(context, userId, client.MatterId, BoundaryWeek52.AddDays(1), 480, true, StandardRate).ConfigureAwait(false);
        await EntryAsync(context, userId, client.MatterId, BoundaryWeek1.AddDays(1), 300, true, StandardRate).ConfigureAwait(false);
        await EntryAsync(context, userId, client.MatterId, BoundaryWeek3.AddDays(1), 360, true, StandardRate).ConfigureAwait(false);

        return client.ClientId;
    }

    /// <summary>Creates a client with one matter to hang entries on.</summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="code">Client code, which is also how ties are broken in row order.</param>
    /// <returns>The client and matter identifiers.</returns>
    private static async Task<(int ClientId, int MatterId)> AddClientAsync(
        LexTimeDbContext context,
        string code)
    {
        var clientId = await DirectSql.InsertClientAsync(context, code).ConfigureAwait(false);
        var matterId = await DirectSql.InsertMatterAsync(context, clientId, "M001").ConfigureAwait(false);

        return (clientId, matterId);
    }

    /// <summary>Records one time entry. A thin alias, so the fixture reads as data.</summary>
    /// <param name="context">Context supplying the connection.</param>
    /// <param name="userId">The timekeeper.</param>
    /// <param name="matterId">The matter, which determines the client.</param>
    /// <param name="workDate">The billing date, which determines the week.</param>
    /// <param name="minutes">Duration; a positive multiple of six.</param>
    /// <param name="isBillable">Whether it counts toward billable totals.</param>
    /// <param name="rate">Rate snapshotted onto the entry.</param>
    /// <returns>A task that completes once the entry has been recorded.</returns>
    private static Task EntryAsync(
        LexTimeDbContext context,
        int userId,
        int matterId,
        DateOnly workDate,
        int minutes,
        bool isBillable,
        decimal rate) =>
        DirectSql.InsertTimeEntryAsync(context, userId, matterId, minutes, workDate, isBillable, rate);
}

/// <summary>Identifiers for the clients the main fixture created.</summary>
/// <param name="BigClientId">The client active in every week.</param>
/// <param name="GapClientId">The client with a multi-week gap.</param>
/// <param name="LateClientId">The client whose first activity is mid-range.</param>
/// <param name="TiedClientId">The client tying with <paramref name="BigClientId"/> in week 2.</param>
/// <param name="SmallClientId">
/// The client with the least activity. Used by the endpoint's filter test because it is never
/// top of its week, so a standing of 1 on every row would be a symptom rather than a result.
/// </param>
internal sealed record RollupFixture(
    int BigClientId,
    int GapClientId,
    int LateClientId,
    int TiedClientId,
    int SmallClientId);
