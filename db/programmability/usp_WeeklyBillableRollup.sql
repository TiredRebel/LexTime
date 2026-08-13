/*
    dbo.usp_WeeklyBillableRollup
    ----------------------------
    Per ISO week and client: billable and non-billable hours, the amount billed, the client's
    running billable total, the change against the prior week, and the client's standing that
    week.

    Contract: specs/003-weekly-billable-rollup/contracts/usp-weekly-billable-rollup.md
    Design notes (R1, R2, R6, R10): specs/003-weekly-billable-rollup/research.md

    Applied by the bootstrap script's apply-procedures step, never by an EF migration
    (constitution P7). ProcedureApplier executes this file as a single SqlCommand, which has
    no batch parser -- so there is no GO anywhere in this file, and nothing may precede the
    CREATE OR ALTER, which must be first in its batch. SET NOCOUNT ON goes inside the body,
    where it is legal.
*/
CREATE OR ALTER PROCEDURE dbo.usp_WeeklyBillableRollup
    @FromDate date,
    @ToDate   date,
    @ClientId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /*
        Weeks are identified by a day-count ordinal rather than by their week number.

        1900-01-01 was a Monday, so DATEDIFF(day, '19000101', d) / 7 numbers consecutive
        Monday-to-Sunday weeks and increments by exactly one per calendar week. Two properties
        matter and no alternative has both:

          * It does not consult SET DATEFIRST or SET LANGUAGE, so the answer does not depend on
            who is calling. DATEPART(weekday, ...) does, and is deliberately unused here.

          * It stays contiguous across a year boundary. The week beginning Monday 2025-12-29 is
            ISO year 2026 week 1 -- a week whose days are mostly in 2025 -- and the week before
            it is 2025 week 52. This ordinal puts them at 6574 and 6573, so "the preceding
            week" is just ordinal - 1. Arithmetic on the ISO week number would ask for week 0
            and find nothing, wrongly, every January.

        The ordinal is internal. Callers get WeekStartDate, IsoYear and IsoWeek, derived from
        it below.
    */
    DECLARE @FirstWeekIndex int = DATEDIFF(day, '19000101', @FromDate) / 7;

    WITH WeeklyTotals AS
    (
        SELECT
            m.ClientId,
            WeekIndex = DATEDIFF(day, '19000101', te.WorkDate) / 7,

            /*
                Minutes are summed as integers and converted once. Converting per row and then
                summing would round 400,000 times and drift; this rounds at the end, never in
                the middle. Durations are multiples of six minutes, so hours are always exact
                multiples of 0.1 and decimal represents them without loss -- which is also why
                nothing here is float.
            */
            BillableMinutes =
                SUM(CASE WHEN te.IsBillable = 1 THEN te.DurationMinutes ELSE 0 END),
            NonBillableMinutes =
                SUM(CASE WHEN te.IsBillable = 0 THEN te.DurationMinutes ELSE 0 END),

            /*
                Rate times minutes inside the sum, divided by 60 outside it. The rate is the one
                snapshotted onto each entry when it was created, never the timekeeper's current
                rate -- joining to Users here would look correct and silently rewrite every
                historical figure the next time someone got a raise.

                This sum is minutes-times-money and is narrowed to decimal(19,4) below, which
                holds about 10^15. A single client-week would have to bill some 10^11 hours to
                reach it. Stated because the narrowing is a real ceiling rather than a
                formality, and a wider target is not free: decimal(38,x) divided by a literal
                runs into SQL Server's precision rules and loses scale instead of erroring.
            */
            BillableMinuteValue =
                SUM(CASE WHEN te.IsBillable = 1
                         THEN te.DurationMinutes * te.HourlyRateSnapshot
                         ELSE 0 END)
        FROM dbo.TimeEntries AS te
        INNER JOIN dbo.Matters AS m
            ON m.MatterId = te.MatterId
        WHERE te.WorkDate >= @FromDate
          AND te.WorkDate <= @ToDate
        /*
            Neither Matters.IsActive nor Clients.IsActive is filtered on, here or below.
            Deactivation is forward-looking: a report on a past period describes what was
            billed then, and a client who left last year still has last year's revenue. This is
            FR-010 and FR-011, and it is a stated position rather than an omission.
        */
        GROUP BY
            m.ClientId,
            DATEDIFF(day, '19000101', te.WorkDate) / 7
    ),
    Windowed AS
    (
        SELECT
            wt.ClientId,
            wt.WeekIndex,
            BillableHours    = CAST(wt.BillableMinutes AS decimal(18, 4)) / 60.0,
            NonBillableHours = CAST(wt.NonBillableMinutes AS decimal(18, 4)) / 60.0,
            BillableAmount   = CAST(wt.BillableMinuteValue AS decimal(19, 4)) / 60.0,

            /*
                Running total of billable hours for one client, oldest week first.

                ROWS UNBOUNDED PRECEDING rather than the default RANGE. Here the two produce
                the same answer, because WeekIndex is unique within a client partition and
                there are no peer rows to accumulate together -- but the default is worth
                stating rather than relying on: RANGE would fold ties into a single step if the
                grain ever changed, and it asks the optimiser for a window spool that ROWS does
                not need.
            */
            CumulativeBillableHours =
                SUM(CAST(wt.BillableMinutes AS decimal(18, 4)) / 60.0) OVER (
                    PARTITION BY wt.ClientId
                    ORDER BY wt.WeekIndex
                    ROWS UNBOUNDED PRECEDING),

            /*
                LAG gives the client's previous *row*, which is not the same thing as the
                previous *week* once a client goes quiet -- rows are only emitted for weeks
                with activity. Both the previous ordinal and the previous hours are carried
                forward so the final SELECT can tell those two cases apart.
            */
            PrevWeekIndex =
                LAG(wt.WeekIndex) OVER (
                    PARTITION BY wt.ClientId
                    ORDER BY wt.WeekIndex),
            PrevBillableHours =
                LAG(CAST(wt.BillableMinutes AS decimal(18, 4)) / 60.0) OVER (
                    PARTITION BY wt.ClientId
                    ORDER BY wt.WeekIndex),

            /*
                Standing within the week, computed here over every client and *before* the
                @ClientId filter is applied in the final SELECT. Ranking after filtering would
                give a single-client report a standing of 1 on every row, which is
                information-free and would still look entirely plausible in a response body.

                DENSE_RANK, not RANK: clients tied on billable hours share a position and the
                next client takes the one immediately after, rather than having positions
                skipped over it.

                Cast to int because the ranking functions return bigint. The contract says int,
                and a standing among a few hundred clients has no use for the other 32 bits --
                but without the cast the caller reads a bigint into an int and the whole result
                set fails on the first row.
            */
            ClientRankInWeek = CAST(
                DENSE_RANK() OVER (
                    PARTITION BY wt.WeekIndex
                    ORDER BY wt.BillableMinutes DESC) AS int)
        FROM WeeklyTotals AS wt
    )
    SELECT
        /*
            ISO-8601 attribution, derived from the week's Monday. A week belongs to the
            week-numbering year containing its Thursday, which SQL Server has no part for --
            hence the explicit +3 days. DATEPART(ISO_WEEK, ...) does supply the number and,
            unlike DATEPART(weekday, ...), genuinely ignores SET DATEFIRST.
        */
        IsoYear  = YEAR(DATEADD(day, 3, DATEADD(day, w.WeekIndex * 7, '19000101'))),
        IsoWeek  = DATEPART(ISO_WEEK, DATEADD(day, w.WeekIndex * 7, '19000101')),
        WeekStartDate = CAST(DATEADD(day, w.WeekIndex * 7, '19000101') AS date),

        w.ClientId,
        ClientCode = c.ClientCode,
        ClientName = c.Name,

        BillableHours           = CAST(w.BillableHours AS decimal(12, 2)),
        NonBillableHours        = CAST(w.NonBillableHours AS decimal(12, 2)),
        BillableAmount          = CAST(w.BillableAmount AS decimal(14, 2)),
        CumulativeBillableHours = CAST(w.CumulativeBillableHours AS decimal(12, 2)),

        /*
            The change against the preceding calendar week. Three cases, and the order they are
            tested in is load-bearing:

              1. The preceding week falls outside the requested range. NULL -- no comparison was
                 possible. Note this is a statement about the range, not about the client: a
                 client whose first activity is in week 5 of a ten-week range gets a number,
                 because weeks 1 to 4 were visible and it was simply silent through them.
                 Tested first because a row can be both the client's first and at the range
                 edge, and the range edge is the stronger claim.

              2. The previous row *is* the previous week. Ordinary subtraction.

              3. The previous week is inside the range but the client has no row in it, so it
                 billed nothing: the change is the whole of this week's hours.

            Case 3 is why LAG alone is not enough, and case 1 is why LAG(..., 1, 0) would be
            wrong -- a zero default silently turns every range-edge row into case 3.
        */
        HoursDeltaVsPriorWeek = CAST(
            CASE
                WHEN w.WeekIndex - 1 < @FirstWeekIndex        THEN NULL
                WHEN w.PrevWeekIndex = w.WeekIndex - 1        THEN w.BillableHours - w.PrevBillableHours
                ELSE                                               w.BillableHours
            END AS decimal(12, 2)),

        w.ClientRankInWeek
    FROM Windowed AS w
    INNER JOIN dbo.Clients AS c
        ON c.ClientId = w.ClientId
    /*
        The catch-all optional parameter. One cached plan serves both the all-clients and the
        single-client call, so whichever compiles first is imposed on the other.

        OPTION (RECOMPILE) is the usual answer and is deliberately not taken yet. The index
        before/after measurement in the next feature needs two comparable plans, and RECOMPILE
        would make every execution's plan depend on the arguments supplied -- turning that
        comparison into a comparison of two differently-compiled plans. Constitution P8 says
        the measurement drives the claim; tuning against an unmeasured problem inverts that.
        Named here so it reads as a decision rather than an oversight.
    */
    WHERE (@ClientId IS NULL OR w.ClientId = @ClientId)
    /*
        Ordering is part of this procedure's contract, not an accident of the plan: callers may
        rely on it and one test asserts two identical calls agree row for row. ClientCode is
        unique, so the order is total even when two clients share a standing.
    */
    ORDER BY
        w.WeekIndex,
        w.ClientRankInWeek,
        c.ClientCode;
END
