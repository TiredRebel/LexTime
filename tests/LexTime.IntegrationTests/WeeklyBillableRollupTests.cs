using System.Data;
using LexTime.Application.Reporting;
using LexTime.Infrastructure.Reporting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the rollup's arithmetic against expectations computed by hand.
/// </summary>
/// <remarks>
/// Constitution P12. Every figure asserted below was worked out on paper from
/// <see cref="RollupFixtureBuilder"/>'s inputs and written here before the procedure was run
/// against them. That order is the whole point: a running total that is wrong is wrong in the
/// same direction on every row, so the result set stays internally consistent and nothing
/// looks out of place. Expectations captured from a run would agree with any implementation,
/// including a broken one.
/// <para>
/// These call the procedure through the reader rather than over HTTP. A test routed through
/// the endpoint asserts the procedure, the reader, the handler, the routing and the serialiser
/// at once and names none of them when it goes red.
/// </para>
/// </remarks>
/// <param name="fixture">Supplies the running SQL Server container.</param>
[Collection(DatabaseCollection.Name)]
public sealed class WeeklyBillableRollupTests(SqlServerFixture fixture)
{
    /// <summary>
    /// The complete result set for the main fixture, row for row and field for field.
    /// </summary>
    /// <remarks>
    /// Twelve rows across five weeks and five clients. Worth reading as a whole: the
    /// individual tests below re-assert single cases from this table so a failure names which
    /// rule broke, but this one is what catches a figure nobody thought to write a test for.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MatchesTheHandComputedFixtureRowForRow()
    {
        var (connectionString, f) = await this.BuildFixtureAsync("LexTime_RollupHandComputed")
            .ConfigureAwait(true);

        var actual = await ReadAsync(connectionString).ConfigureAwait(true);

        var expected = new[]
        {
            // Week 1 (ISO 2026-W02). Range starts on this Monday, so no change is measurable
            // for anyone: the preceding week is outside the range, which is not the same
            // statement as "nothing was billed then".
            Row(2, RollupFixtureBuilder.Week1, f.BigClientId, "BIG", 10.00m, 0.00m, 5000.00m, 10.00m, null, 1),
            Row(2, RollupFixtureBuilder.Week1, f.GapClientId, "GAP", 8.00m, 0.00m, 3200.00m, 8.00m, null, 2),

            // Week 2 (ISO 2026-W03). BIG and TIED both log 12 billable hours and share the top
            // position; QUIET takes the one immediately after it, not the one after that.
            // TIED and QUIET are each in their first week, but week 1 is inside the range and
            // they were simply silent through it — so their change is a number, not absent.
            // QUIET's is 0.00 because zero hours minus a silent week is still zero.
            Row(3, RollupFixtureBuilder.Week2, f.BigClientId, "BIG", 12.00m, 0.00m, 6000.00m, 22.00m, 2.00m, 1),
            Row(3, RollupFixtureBuilder.Week2, f.TiedClientId, "TIED", 12.00m, 0.00m, 4800.00m, 12.00m, 12.00m, 1),
            Row(3, RollupFixtureBuilder.Week2, f.SmallClientId, "QUIET", 0.00m, 5.00m, 0.00m, 0.00m, 0.00m, 2),

            // Week 3 (ISO 2026-W04). QUIET's change is measured against a week in which it has
            // a row carrying zero billable hours — the contiguous case, not the silent one.
            Row(4, RollupFixtureBuilder.Week3, f.BigClientId, "BIG", 8.00m, 0.00m, 4000.00m, 30.00m, -4.00m, 1),
            Row(4, RollupFixtureBuilder.Week3, f.LateClientId, "LATE", 4.00m, 0.00m, 1600.00m, 4.00m, 4.00m, 2),
            Row(4, RollupFixtureBuilder.Week3, f.SmallClientId, "QUIET", 2.00m, 0.00m, 800.00m, 2.00m, 2.00m, 3),

            // Week 4 (ISO 2026-W05). GAP returns after two silent weeks: its change is its own
            // 3 hours, and emphatically not 3 - 8 = -5 against the week it last billed in.
            Row(5, RollupFixtureBuilder.Week4, f.LateClientId, "LATE", 6.00m, 0.00m, 2400.00m, 10.00m, 2.00m, 1),
            Row(5, RollupFixtureBuilder.Week4, f.BigClientId, "BIG", 5.00m, 0.00m, 2500.00m, 35.00m, -3.00m, 2),
            Row(5, RollupFixtureBuilder.Week4, f.GapClientId, "GAP", 3.00m, 0.00m, 1200.00m, 11.00m, 3.00m, 3),

            // Week 5 (ISO 2026-W06), partial: the range ends on the Wednesday. BIG's Tuesday
            // entry counts and its Thursday entry does not, so this row is 6 hours and not 21.
            Row(6, RollupFixtureBuilder.Week5, f.BigClientId, "BIG", 6.00m, 0.00m, 3000.00m, 41.00m, 1.00m, 1),
        };

        Assert.Equal(expected.Length, actual.Count);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    /// <summary>
    /// A client returning after a gap is compared against the silent week before it, not
    /// against the week it last billed in.
    /// </summary>
    /// <remarks>
    /// The single most important assertion in this feature, and the reason the fixture gives
    /// <c>GAP</c> a different non-zero total either side of its gap. Both assertions below are
    /// required: the first alone would also pass under the wrong implementation if those two
    /// numbers ever happened to coincide, and a fixture that let them coincide would look
    /// exactly like a passing test (FR-008, FR-022).
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MeasuresAReturningClientAgainstTheSilentWeekNotItsLastBilledWeek()
    {
        var (connectionString, f) = await this.BuildFixtureAsync("LexTime_RollupGap")
            .ConfigureAwait(true);

        var rows = await ReadAsync(connectionString).ConfigureAwait(true);

        var beforeGap = rows.Single(r =>
            r.ClientId == f.GapClientId && r.WeekStartDate == RollupFixtureBuilder.Week1);
        var afterGap = rows.Single(r =>
            r.ClientId == f.GapClientId && r.WeekStartDate == RollupFixtureBuilder.Week4);

        // The silent weeks produce no rows of their own.
        Assert.DoesNotContain(rows, r =>
            r.ClientId == f.GapClientId
            && (r.WeekStartDate == RollupFixtureBuilder.Week2
                || r.WeekStartDate == RollupFixtureBuilder.Week3));

        Assert.Equal(8.00m, beforeGap.BillableHours);
        Assert.Equal(3.00m, afterGap.BillableHours);

        // Correct: the preceding calendar week was silent, so it counts as zero.
        Assert.Equal(3.00m, afterGap.HoursDeltaVsPriorWeek);

        // Wrong, and stated explicitly so the two readings are held apart rather than left to
        // coincide: this is what a plain LAG over the result set would have produced.
        Assert.NotEqual(
            afterGap.BillableHours - beforeGap.BillableHours,
            afterGap.HoursDeltaVsPriorWeek);
    }

    /// <summary>
    /// A gap spanning the turn of the year is measured correctly, and the weeks are attributed
    /// to the week-numbering year rather than the calendar year of their dates.
    /// </summary>
    /// <remarks>
    /// FR-023, and the case where two rules meet badly. The week beginning Monday 2025-12-29
    /// is ISO year 2026 week 1, and the week before it is 2025 week 52 — so a "preceding week"
    /// derived from the week number would ask for week 0, find nothing, treat the week as
    /// silent and report +5.00 instead of -3.00. Neither the plain gap test above nor a plain
    /// year-attribution check detects that on its own.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task MeasuresAcrossAYearBoundaryUsingDatesNotWeekNumbers()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupYearBoundary").ConfigureAwait(true);

        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            await RollupFixtureBuilder.PopulateYearBoundaryAsync(context).ConfigureAwait(true);
        }

        var rows = await ReadAsync(
            connectionString,
            RollupFixtureBuilder.BoundaryRangeStart,
            RollupFixtureBuilder.BoundaryRangeEnd).ConfigureAwait(true);

        Assert.Equal(3, rows.Count);

        var week52 = rows[0];
        Assert.Equal(2025, week52.IsoYear);
        Assert.Equal(52, week52.IsoWeek);
        Assert.Equal(RollupFixtureBuilder.BoundaryWeek52, week52.WeekStartDate);
        Assert.Equal(8.00m, week52.BillableHours);
        Assert.Null(week52.HoursDeltaVsPriorWeek);

        // A week whose days are mostly in December 2025, belonging to week-numbering year 2026.
        var week1 = rows[1];
        Assert.Equal(2026, week1.IsoYear);
        Assert.Equal(1, week1.IsoWeek);
        Assert.Equal(RollupFixtureBuilder.BoundaryWeek1, week1.WeekStartDate);
        Assert.Equal(5.00m, week1.BillableHours);
        Assert.Equal(13.00m, week1.CumulativeBillableHours);

        // The assertion the whole test exists for. Week-number arithmetic gives +5.00 here.
        Assert.Equal(-3.00m, week1.HoursDeltaVsPriorWeek);

        // ...and the gap branch still works on the far side of the boundary: week 2 is silent.
        var week3 = rows[2];
        Assert.Equal(2026, week3.IsoYear);
        Assert.Equal(3, week3.IsoWeek);
        Assert.Equal(6.00m, week3.BillableHours);
        Assert.Equal(6.00m, week3.HoursDeltaVsPriorWeek);
        Assert.Equal(19.00m, week3.CumulativeBillableHours);
    }

    /// <summary>
    /// A week of purely non-billable work still produces a row.
    /// </summary>
    /// <remarks>
    /// Omitting it would hide work that was done. The client also has to keep a standing that
    /// week, at the bottom rather than absent (FR-022).
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReportsAWeekOfPurelyNonBillableWork()
    {
        var (connectionString, f) = await this.BuildFixtureAsync("LexTime_RollupNonBillable")
            .ConfigureAwait(true);

        var rows = await ReadAsync(connectionString).ConfigureAwait(true);

        var row = rows.Single(r =>
            r.ClientId == f.SmallClientId && r.WeekStartDate == RollupFixtureBuilder.Week2);

        Assert.Equal(0.00m, row.BillableHours);
        Assert.Equal(5.00m, row.NonBillableHours);
        Assert.Equal(0.00m, row.BillableAmount);
        Assert.Equal(2, row.ClientRankInWeek);
    }

    /// <summary>
    /// Clients tied on billable hours share a standing, and the next takes the position
    /// immediately after it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SharesAStandingBetweenTiedClientsWithoutSkippingThePositionBelow()
    {
        var (connectionString, f) = await this.BuildFixtureAsync("LexTime_RollupTie")
            .ConfigureAwait(true);

        var week2 = (await ReadAsync(connectionString).ConfigureAwait(true))
            .Where(r => r.WeekStartDate == RollupFixtureBuilder.Week2)
            .ToList();

        Assert.Equal(1, week2.Single(r => r.ClientId == f.BigClientId).ClientRankInWeek);
        Assert.Equal(1, week2.Single(r => r.ClientId == f.TiedClientId).ClientRankInWeek);

        // Dense, not sparse: two clients at the top do not push the third to position 3.
        Assert.Equal(2, week2.Single(r => r.ClientId == f.SmallClientId).ClientRankInWeek);
    }

    /// <summary>
    /// A range boundary falling mid-week reports that week with its in-range days only.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CountsOnlyTheInRangeDaysOfAPartialWeek()
    {
        var (connectionString, f) = await this.BuildFixtureAsync("LexTime_RollupPartialWeek")
            .ConfigureAwait(true);

        var rows = await ReadAsync(connectionString).ConfigureAwait(true);

        var partial = rows.Single(r => r.WeekStartDate == RollupFixtureBuilder.Week5);

        Assert.Equal(f.BigClientId, partial.ClientId);

        // 6 hours from the Tuesday. The 15-hour Thursday entry falls one day past the range and
        // must not reach any figure — not the week, not the running total.
        Assert.Equal(6.00m, partial.BillableHours);
        Assert.Equal(41.00m, partial.CumulativeBillableHours);
    }

    /// <summary>
    /// A range containing no entries returns no rows and does not fail.
    /// </summary>
    /// <remarks>
    /// FR-024. Calculations that accumulate across rows commonly break on the empty case in a
    /// way no populated test detects, because there is no row on which to notice.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsNoRowsForARangeWithNoEntries()
    {
        var (connectionString, _) = await this.BuildFixtureAsync("LexTime_RollupEmpty")
            .ConfigureAwait(true);

        var rows = await ReadAsync(
            connectionString,
            new DateOnly(2030, 1, 7),
            new DateOnly(2030, 2, 4)).ConfigureAwait(true);

        Assert.Empty(rows);
    }

    /// <summary>
    /// Two identical calls return identical rows in an identical order.
    /// </summary>
    /// <remarks>
    /// SC-003. Ordering is part of the procedure's contract rather than an accident of the
    /// plan, and the fixture contains a tie — the case where an unstable sort would show.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReturnsTheSameRowsInTheSameOrderOnRepeatedCalls()
    {
        var (connectionString, _) = await this.BuildFixtureAsync("LexTime_RollupDeterminism")
            .ConfigureAwait(true);

        var first = await ReadAsync(connectionString).ConfigureAwait(true);
        var second = await ReadAsync(connectionString).ConfigureAwait(true);

        Assert.Equal(first.Count, second.Count);

        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
        }
    }

    /// <summary>
    /// A client that is inactive today still appears for the weeks in which it billed.
    /// </summary>
    /// <remarks>
    /// FR-010 and SC-007, the position feature 002's Dependencies section required this spec to
    /// state. Deactivation is forward-looking, so a report on a past period describes what was
    /// billed then. This test is what stops a later "filter out inactive clients" change from
    /// passing quietly.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task IncludesAClientThatIsInactiveNowButBilledInThePeriod()
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync("LexTime_RollupInactive").ConfigureAwait(true);

        int retiredClientId;
        await using (var context = SqlServerFixture.CreateContext(connectionString))
        {
            var f = await RollupFixtureBuilder.PopulateAsync(context).ConfigureAwait(true);
            retiredClientId = f.GapClientId;

            // The client stops trading after the reported period. Its history stays.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.Clients SET IsActive = 0 WHERE ClientId = @id;",
                new SqlParameter("@id", retiredClientId)).ConfigureAwait(true);
        }

        var rows = await ReadAsync(connectionString).ConfigureAwait(true);

        Assert.Contains(rows, r => r.ClientId == retiredClientId);
        Assert.Equal(
            8.00m,
            rows.Single(r =>
                r.ClientId == retiredClientId
                && r.WeekStartDate == RollupFixtureBuilder.Week1).BillableHours);
    }

    /// <summary>
    /// The procedure returns every derived figure already computed, with no application code
    /// involved at all.
    /// </summary>
    /// <remarks>
    /// SC-009, and the observable form of FR-014. This deliberately uses raw ADO.NET rather
    /// than the reader: a design that fetched flat rows and derived the running total, the
    /// change and the standing in C# would satisfy every other test in this class and fail
    /// here, which is the only place that difference is visible.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ComputesEveryDerivedFigureInTheDatabase()
    {
        var (connectionString, _) = await this.BuildFixtureAsync("LexTime_RollupInDatabase")
            .ConfigureAwait(true);

        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand(
            SqlWeeklyBillableRollupReader.ProcedureName,
            connection)
        {
            CommandType = CommandType.StoredProcedure,
        };

        command.Parameters.Add("@FromDate", SqlDbType.Date).Value =
            RollupFixtureBuilder.RangeStart.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@ToDate", SqlDbType.Date).Value =
            RollupFixtureBuilder.RangeEnd.ToDateTime(TimeOnly.MinValue);

        await connection.OpenAsync().ConfigureAwait(true);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(true);

        Assert.True(await reader.ReadAsync().ConfigureAwait(true));

        // The three cross-row figures arrive finished, from a single call, with nothing between
        // the database and this assertion.
        Assert.Equal(10.00m, reader.GetDecimal(reader.GetOrdinal("CumulativeBillableHours")));
        Assert.True(reader.IsDBNull(reader.GetOrdinal("HoursDeltaVsPriorWeek")));
        Assert.Equal(1, reader.GetInt32(reader.GetOrdinal("ClientRankInWeek")));
        Assert.Equal(12, reader.FieldCount);
    }

    /// <summary>Builds an expected row, filling in the parts the fixture makes predictable.</summary>
    /// <param name="isoWeek">ISO week number; every main-fixture week is in ISO year 2026.</param>
    /// <param name="weekStart">The week's Monday.</param>
    /// <param name="clientId">The generated client identifier.</param>
    /// <param name="clientCode">The client's code, which also decides order within a tie.</param>
    /// <param name="billableHours">Expected billable hours.</param>
    /// <param name="nonBillableHours">Expected non-billable hours.</param>
    /// <param name="amount">Expected amount billed.</param>
    /// <param name="cumulative">Expected running billable total.</param>
    /// <param name="delta">Expected change against the prior week; null at the range edge.</param>
    /// <param name="rank">Expected standing within the week.</param>
    /// <returns>The row the procedure is expected to return.</returns>
    private static WeeklyBillableRollupRow Row(
        int isoWeek,
        DateOnly weekStart,
        int clientId,
        string clientCode,
        decimal billableHours,
        decimal nonBillableHours,
        decimal amount,
        decimal cumulative,
        decimal? delta,
        int rank) =>
        new(
            2026,
            isoWeek,
            weekStart,
            clientId,
            clientCode,
            $"Client {clientCode}",
            billableHours,
            nonBillableHours,
            amount,
            cumulative,
            delta,
            rank);

    /// <summary>Creates an isolated database and populates the main fixture in it.</summary>
    /// <param name="databaseName">Name for the isolated database.</param>
    /// <returns>Its connection string and the identifiers the fixture generated.</returns>
    private async Task<(string ConnectionString, RollupFixture Fixture)> BuildFixtureAsync(
        string databaseName)
    {
        var connectionString = await fixture
            .CreateIsolatedDatabaseAsync(databaseName).ConfigureAwait(false);

        await using var context = SqlServerFixture.CreateContext(connectionString);
        var built = await RollupFixtureBuilder.PopulateAsync(context).ConfigureAwait(false);

        return (connectionString, built);
    }

    /// <summary>Runs the rollup over the fixture's range unless another is given.</summary>
    /// <param name="connectionString">The database to report on.</param>
    /// <param name="from">Range start; defaults to the main fixture's.</param>
    /// <param name="to">Range end; defaults to the main fixture's.</param>
    /// <returns>The rows the procedure returned, in its own order.</returns>
    private static Task<IReadOnlyList<WeeklyBillableRollupRow>> ReadAsync(
        string connectionString,
        DateOnly? from = null,
        DateOnly? to = null) =>
        new SqlWeeklyBillableRollupReader(connectionString).ReadAsync(
            new WeeklyBillableRollupQuery(
                from ?? RollupFixtureBuilder.RangeStart,
                to ?? RollupFixtureBuilder.RangeEnd,
                null));
}
