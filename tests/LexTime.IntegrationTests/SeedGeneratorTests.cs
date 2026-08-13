using LexTime.Infrastructure.Seeding;

namespace LexTime.IntegrationTests;

/// <summary>
/// Pins the generator's determinism and its distribution properties.
/// </summary>
/// <remarks>
/// No container, no database, no I/O — the generator is a pure function of
/// <see cref="SeedOptions"/>, so these run in milliseconds at a hundredth of production
/// scale. That matters: a regression in generation is introduced here, not in the loader,
/// and a test that needed a three-minute load would not be run often enough to catch it.
/// </remarks>
public sealed class SeedGeneratorTests
{
    /// <summary>Production shape at 1/100 scale, so the same invariants apply.</summary>
    private static readonly SeedOptions Small = new()
    {
        UserCount = 25,
        ClientCount = 60,
        MatterCount = 220,
        TimeEntryCount = 4_000,
    };

    /// <summary>
    /// Two generations from identical options produce identical rows.
    /// </summary>
    /// <remarks>
    /// The single most important assertion in this feature. Constitution P8 requires the
    /// index before/after measurement to be comparable between runs, and it cannot be if
    /// the dataset underneath differs. A generator that drifts breaks that
    /// silently — nothing fails, the numbers are just quietly meaningless.
    /// </remarks>
    [Fact]
    public void GeneratesIdenticalDataFromIdenticalOptions()
    {
        var first = SeedDataGenerator.Generate(Small);
        var second = SeedDataGenerator.Generate(Small);

        Assert.Equal(first.Users.Count, second.Users.Count);
        Assert.Equal(first.Entries.Count, second.Entries.Count);

        for (var i = 0; i < first.Entries.Count; i++)
        {
            Assert.Equal(first.Entries[i], second.Entries[i]);
        }

        for (var i = 0; i < first.Matters.Count; i++)
        {
            Assert.Equal(first.Matters[i], second.Matters[i]);
        }

        for (var i = 0; i < first.Users.Count; i++)
        {
            Assert.Equal(first.Users[i].Email, second.Users[i].Email);
            Assert.Equal(first.Users[i].DefaultHourlyRate, second.Users[i].DefaultHourlyRate);
            Assert.Equal(first.Users[i].IsActive, second.Users[i].IsActive);
        }
    }

    /// <summary>A different seed produces a different dataset, so the seed is actually used.</summary>
    /// <remarks>
    /// Without this, a generator that ignored its seed entirely would satisfy the
    /// determinism test perfectly.
    /// </remarks>
    [Fact]
    public void GeneratesDifferentDataFromADifferentSeed()
    {
        var first = SeedDataGenerator.Generate(Small);
        var second = SeedDataGenerator.Generate(Small with { RandomSeed = Small.RandomSeed + 1 });

        Assert.NotEqual(first.Entries[0], second.Entries[0]);
    }

    /// <summary>Every generated entry satisfies the duration rules the schema enforces.</summary>
    [Fact]
    public void GeneratesOnlyValidDurations()
    {
        var data = SeedDataGenerator.Generate(Small);

        Assert.DoesNotContain(
            data.Entries,
            e => e.DurationMinutes <= 0 || e.DurationMinutes % 6 != 0 || e.DurationMinutes > 1440);
    }

    /// <summary>
    /// No entry is dated after the reference date, and the oldest sits at the far edge of
    /// the 24-month window rather than short of it.
    /// </summary>
    [Fact]
    public void GeneratesDatesWithinTheWindow()
    {
        var data = SeedDataGenerator.Generate(Small);

        Assert.DoesNotContain(data.Entries, e => e.WorkDate > Small.ReferenceDate);
        Assert.DoesNotContain(data.Entries, e => e.WorkDate < Small.EarliestDate);
    }

    /// <summary>Weekend activity is a small minority, not the two sevenths uniform dates would give.</summary>
    [Fact]
    public void ConcentratesEntriesOnWeekdays()
    {
        var data = SeedDataGenerator.Generate(Small);

        var weekend = data.Entries.Count(e =>
            e.WorkDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        var share = 100.0 * weekend / data.Entries.Count;

        Assert.True(share < 10, $"Weekend share was {share:0.##}%, expected under 10%.");
    }

    /// <summary>A meaningful minority of entries are non-billable.</summary>
    [Fact]
    public void GeneratesANonBillableMinority()
    {
        var data = SeedDataGenerator.Generate(Small);

        var share = 100.0 * data.Entries.Count(e => !e.IsBillable) / data.Entries.Count;

        Assert.InRange(share, 10, 25);
    }

    /// <summary>
    /// Activity is heavily skewed across clients: a few carry most of the logged minutes.
    /// </summary>
    /// <remarks>
    /// Uniform activity would make the rollup's ranking column meaningless and produce
    /// query plans that do not differ interestingly with or without an index — which is the
    /// whole point of the dataset (constitution P9).
    /// </remarks>
    [Fact]
    public void SkewsActivityAcrossClients()
    {
        var data = SeedDataGenerator.Generate(Small);

        var minutesByClient = data.Entries
            .GroupBy(e => data.Matters[e.MatterIndex].ClientIndex)
            .Select(g => (long)g.Sum(e => e.DurationMinutes))
            .OrderByDescending(m => m)
            .ToList();

        var topTen = minutesByClient.Take(10).Sum();
        var share = 100.0 * topTen / minutesByClient.Sum();

        Assert.True(share >= 50, $"Top ten clients held {share:0.##}% of minutes, expected at least 50%.");
    }

    /// <summary>
    /// Matter numbers repeat across clients.
    /// </summary>
    /// <remarks>
    /// Deliberate. Numbers repeating across clients is exactly what feature 001's composite
    /// unique index exists to permit, and a generator producing globally unique numbers
    /// would leave that index unexercised at volume — the one modelling error the schema was
    /// written to prevent.
    /// </remarks>
    [Fact]
    public void ReusesMatterNumbersAcrossDifferentClients()
    {
        var data = SeedDataGenerator.Generate(Small);

        var duplicatedAcrossClients = data.Matters
            .GroupBy(m => m.MatterNumber)
            .Any(g => g.Select(m => m.ClientIndex).Distinct().Count() > 1);

        Assert.True(duplicatedAcrossClients, "No matter number was reused across clients.");
    }

    /// <summary>
    /// Inactive users, clients and matters exist, and at least one inactive client carries
    /// history.
    /// </summary>
    /// <remarks>
    /// This is the fixture the active-matter domain rule will be tested against, and the
    /// case feature 003's rollup takes a position on in its FR-010: a client who left last
    /// year still has billable activity in last year's report.
    /// </remarks>
    [Fact]
    public void GeneratesInactiveRowsIncludingSomeWithHistory()
    {
        var data = SeedDataGenerator.Generate(Small);

        Assert.Contains(data.Users, u => !u.IsActive);
        Assert.Contains(data.Clients, c => !c.IsActive);
        Assert.Contains(data.Matters, m => !m.IsActive);

        var inactiveClientIndices = data.Clients
            .Select((client, index) => (client, index))
            .Where(pair => !pair.client.IsActive)
            .Select(pair => pair.index)
            .ToHashSet();

        var inactiveWithHistory = data.Entries
            .Select(e => data.Matters[e.MatterIndex].ClientIndex)
            .Any(inactiveClientIndices.Contains);

        Assert.True(inactiveWithHistory, "No inactive client carried any history.");
    }
}
