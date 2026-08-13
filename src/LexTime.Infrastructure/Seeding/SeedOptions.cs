namespace LexTime.Infrastructure.Seeding;

/// <summary>
/// Every input the seed generator has. Nothing else may influence what it produces.
/// </summary>
/// <remarks>
/// Two properties of this type carry the weight of the whole feature.
/// <para>
/// First, it is the <em>only</em> source of variation. The generator reads no clock, no
/// machine entropy and no environment — given the same options it produces the same rows,
/// which is what makes feature 003's index measurement comparable between runs
/// (constitution P8, FR-020).
/// </para>
/// <para>
/// Second, the volumes are parameters rather than constants. That is what lets the tests
/// exercise the real generator at a hundredth of the scale instead of asserting the seed's
/// shape only after a three-minute load.
/// </para>
/// </remarks>
public sealed record SeedOptions
{
    /// <summary>
    /// The newest date any generated entry may carry, and the anchor every other date is
    /// an offset from.
    /// </summary>
    /// <remarks>
    /// Not free to change. Feature 001 shipped
    /// <c>WorkDateConstraintTests.AcceptsWorkDateAtTheOldestSeededBoundary</c>, which
    /// asserts that 2024-08-13 is accepted as the far edge of what this feature seeds. A
    /// 24-month window back from 2026-08-13 lands exactly there. Moving this anchor without
    /// moving that test leaves a test that still passes and no longer means anything.
    /// </remarks>
    public static readonly DateOnly DefaultReferenceDate = new(2026, 8, 13);

    /// <summary>Number of timekeepers to generate.</summary>
    public int UserCount { get; init; } = 25;

    /// <summary>Number of clients to generate.</summary>
    public int ClientCount { get; init; } = 60;

    /// <summary>Number of matters to generate, distributed unevenly across clients.</summary>
    public int MatterCount { get; init; } = 220;

    /// <summary>
    /// Number of time entries to generate. The only volume tests reduce meaningfully.
    /// </summary>
    public int TimeEntryCount { get; init; } = 400_000;

    /// <summary>How far back from <see cref="ReferenceDate"/> entries may be dated.</summary>
    public int MonthsOfHistory { get; init; } = 24;

    /// <summary>The newest permitted entry date. See <see cref="DefaultReferenceDate"/>.</summary>
    public DateOnly ReferenceDate { get; init; } = DefaultReferenceDate;

    /// <summary>
    /// Seed for the single pseudo-random generator. Committed, so that two runs anywhere
    /// produce identical data.
    /// </summary>
    public int RandomSeed { get; init; } = 20260813;

    /// <summary>
    /// Fraction of users, clients and matters marked inactive. Some of them keep historical
    /// entries, which is the fixture the active-matter rule will be tested against and the
    /// case feature 003's rollup has to take a position on (FR-016, FR-017).
    /// </summary>
    public double InactiveShare { get; init; } = 0.12;

    /// <summary>Fraction of entries recorded as non-billable (FR-015).</summary>
    public double NonBillableShare { get; init; } = 0.18;

    /// <summary>
    /// Fraction of entries falling on a Saturday or Sunday. Well below two sevenths on
    /// purpose: uniformly distributed dates produce uniform query plans and a rollup nobody
    /// would believe (constitution P9, FR-013).
    /// </summary>
    public double WeekendShare { get; init; } = 0.05;

    /// <summary>The oldest date an entry may carry, derived from the anchor and the window.</summary>
    public DateOnly EarliestDate => this.ReferenceDate.AddMonths(-this.MonthsOfHistory);
}
