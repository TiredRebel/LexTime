using System.Globalization;
using LexTime.Domain.Entities;

namespace LexTime.Infrastructure.Seeding;

/// <summary>
/// Produces the seed dataset from <see cref="SeedOptions"/> and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Nothing here may read ambient state.</strong> No <c>DateTime.Now</c>, no
/// <c>DateTime.UtcNow</c>, no <c>Random.Shared</c>, no <c>Guid.NewGuid</c>. Given identical
/// options this produces identical rows, which is what makes feature 003's index
/// before/after measurement comparable between runs (constitution P8, FR-020, FR-021).
/// </para>
/// <para>
/// It also performs no I/O and touches no database, so its shape and its determinism are
/// testable at a hundredth of the scale in milliseconds — which is where a regression would
/// actually be introduced.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA5394:Do not use insecure randomness",
    Justification =
        "Reproducibility is the requirement. This generator produces demonstration data, " +
        "never keys, tokens or identifiers, and FR-020 requires two runs from the same seed " +
        "to produce identical rows so that feature 003's index measurement is comparable. " +
        "A cryptographic generator cannot be seeded to repeat and would make that " +
        "impossible. Reviewed as a P24 item; recorded in docs/agent-log.md.")]
public static class SeedDataGenerator
{
    private static readonly string[] MatterSubjects =
    [
        "Acquisition", "Employment dispute", "Lease review", "Regulatory filing",
        "Shareholder agreement", "Trademark opposition", "Supply contract",
        "Data protection audit", "Restructuring", "Litigation",
    ];

    private static readonly string[] NarrativeVerbs =
    [
        "Reviewed", "Drafted", "Revised", "Attended call regarding", "Researched",
        "Prepared", "Corresponded on", "Analysed",
    ];

    /// <summary>
    /// Durations weighted towards short entries, because most of a timekeeper's day is
    /// fragments. All are multiples of six; none approaches the 1440 ceiling, because a
    /// single 24-hour entry is not a thing that happens.
    /// </summary>
    private static readonly int[] DurationPool =
        [6, 6, 6, 12, 12, 18, 24, 30, 30, 36, 48, 60, 60, 90, 120, 180, 240];

    /// <summary>
    /// Generates the whole dataset.
    /// </summary>
    /// <param name="options">Volumes, shares, reference date and generator seed.</param>
    /// <returns>Users, clients, matters and entries, none of which carry database keys yet.</returns>
    public static SeedDataSet Generate(SeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // One generator, seeded from options. Every draw below comes from it in a fixed
        // order, so the whole dataset is a pure function of the seed.
        var random = new Random(options.RandomSeed);

        var users = GenerateUsers(options, random);
        var clients = GenerateClients(options, random);
        var matters = GenerateMatters(options, random);
        var weights = BuildClientWeights(options, random);
        var entries = GenerateEntries(options, random, users, matters, weights);

        return new SeedDataSet(users, clients, matters, entries);
    }

    /// <summary>Generates timekeepers with a spread of rates and a minority inactive.</summary>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <returns>The generated users.</returns>
    private static List<User> GenerateUsers(SeedOptions options, Random random)
    {
        var createdAt = options.EarliestDate.ToDateTime(TimeOnly.MinValue).AddDays(-30);
        var users = new List<User>(options.UserCount);

        for (var i = 0; i < options.UserCount; i++)
        {
            // Rates spread across seniority rather than drawn uniformly: partners and
            // juniors do not bill alike, and a uniform rate makes billable amount a
            // constant multiple of hours, which would make the rollup's amount column
            // carry no information.
            var band = i % 4;
            var rate = band switch
            {
                0 => 180m,
                1 => 275m,
                2 => 420m,
                _ => 650m,
            } + (random.Next(0, 10) * 5m);

            users.Add(new User
            {
                Email = string.Create(
                    CultureInfo.InvariantCulture, $"timekeeper{i + 1:000}@lextime.test"),
                FullName = string.Create(CultureInfo.InvariantCulture, $"Timekeeper {i + 1:000}"),
                DefaultHourlyRate = rate,
                IsActive = random.NextDouble() >= options.InactiveShare,
                CreatedAtUtc = createdAt,
            });
        }

        // At least one inactive user, whatever the draw produced, so the fixture the
        // active-matter rule needs is guaranteed rather than probable (SC-007).
        users[^1].IsActive = false;

        return users;
    }

    /// <summary>Generates clients with a minority inactive.</summary>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <returns>The generated clients.</returns>
    private static List<Client> GenerateClients(SeedOptions options, Random random)
    {
        var createdAt = options.EarliestDate.ToDateTime(TimeOnly.MinValue).AddDays(-30);
        var clients = new List<Client>(options.ClientCount);

        for (var i = 0; i < options.ClientCount; i++)
        {
            clients.Add(new Client
            {
                ClientCode = string.Create(CultureInfo.InvariantCulture, $"CL{i + 1:000}"),
                Name = string.Create(CultureInfo.InvariantCulture, $"Client {i + 1:000} Holdings"),
                IsActive = random.NextDouble() >= options.InactiveShare,
                CreatedAtUtc = createdAt,
            });
        }

        // Guarantee an inactive client that will still receive history below — the case
        // feature 003's rollup must take a position on (SC-007).
        clients[0].IsActive = false;

        return clients;
    }

    /// <summary>
    /// Generates matters, distributed unevenly across clients.
    /// </summary>
    /// <remarks>
    /// Matter numbers restart at 001 for every client. That is deliberate: numbers repeating
    /// across clients is exactly what feature 001's composite unique index exists to permit,
    /// and generating globally unique numbers would leave it unexercised at volume.
    /// </remarks>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <returns>The generated matters.</returns>
    private static List<MatterDraft> GenerateMatters(SeedOptions options, Random random)
    {
        var createdAt = options.EarliestDate.ToDateTime(TimeOnly.MinValue).AddDays(-15);
        var matters = new List<MatterDraft>(options.MatterCount);
        var perClient = new int[options.ClientCount];

        for (var i = 0; i < options.MatterCount; i++)
        {
            // Squaring a uniform draw biases towards low indices, so early clients collect
            // disproportionately many matters and the tail collects one or two.
            var draw = random.NextDouble();
            var clientIndex = (int)(draw * draw * options.ClientCount);
            clientIndex = Math.Min(clientIndex, options.ClientCount - 1);

            perClient[clientIndex]++;

            matters.Add(new MatterDraft(
                ClientIndex: clientIndex,
                MatterNumber: string.Create(
                    CultureInfo.InvariantCulture, $"{perClient[clientIndex]:000}"),
                Name: MatterSubjects[random.Next(MatterSubjects.Length)],
                IsBillableByDefault: random.NextDouble() > 0.05,
                IsActive: random.NextDouble() >= options.InactiveShare,
                CreatedAtUtc: createdAt));
        }

        return matters;
    }

    /// <summary>
    /// Assigns each client a share of activity, heavily skewed.
    /// </summary>
    /// <remarks>
    /// A handful of clients must account for most logged minutes (FR-014, SC-004). Uniform
    /// activity produces a rollup whose ranking column is meaningless and query plans that
    /// do not differ interestingly with or without an index.
    /// </remarks>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <returns>A cumulative weight table over client indices.</returns>
    private static double[] BuildClientWeights(SeedOptions options, Random random)
    {
        var weights = new double[options.ClientCount];
        var total = 0.0;

        for (var i = 0; i < options.ClientCount; i++)
        {
            // Roughly Zipf-shaped: the first client carries an order of magnitude more than
            // the last, with jitter so the ordering is not perfectly monotonic.
            var weight = (1.0 / (i + 1.0)) * (0.75 + (random.NextDouble() * 0.5));
            total += weight;
            weights[i] = total;
        }

        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }

        return weights;
    }

    /// <summary>Generates time entries with weekday concentration and client skew.</summary>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <param name="users">Generated users, for rate snapshots.</param>
    /// <param name="matters">Generated matters, for client attribution.</param>
    /// <param name="clientWeights">Cumulative client weights.</param>
    /// <returns>The generated entries.</returns>
    private static List<TimeEntryDraft> GenerateEntries(
        SeedOptions options,
        Random random,
        List<User> users,
        List<MatterDraft> matters,
        double[] clientWeights)
    {
        var mattersByClient = BuildMatterLookup(options.ClientCount, matters);
        var totalDays = options.ReferenceDate.DayNumber - options.EarliestDate.DayNumber;
        var entries = new List<TimeEntryDraft>(options.TimeEntryCount);

        for (var i = 0; i < options.TimeEntryCount; i++)
        {
            var clientIndex = PickWeighted(clientWeights, random.NextDouble());
            var candidates = mattersByClient[clientIndex];
            if (candidates.Count == 0)
            {
                // A client that drew no matters cannot receive entries. Fall back rather
                // than skip, so the requested entry count is produced exactly and the
                // state inspector's equality check stays meaningful.
                candidates = mattersByClient.First(m => m.Count > 0);
            }

            var matterIndex = candidates[random.Next(candidates.Count)];
            var userIndex = random.Next(users.Count);
            var workDate = PickWorkDate(options, random, totalDays);
            var duration = DurationPool[random.Next(DurationPool.Length)];

            entries.Add(new TimeEntryDraft(
                UserIndex: userIndex,
                MatterIndex: matterIndex,
                WorkDate: workDate,
                DurationMinutes: duration,
                IsBillable: random.NextDouble() >= options.NonBillableShare,
                HourlyRateSnapshot: users[userIndex].DefaultHourlyRate,
                Narrative: string.Create(
                    CultureInfo.InvariantCulture,
                    $"{NarrativeVerbs[random.Next(NarrativeVerbs.Length)]} {matters[matterIndex].Name.ToLowerInvariant()}"),

                // Recorded a day or two after the work, not all at one instant. Derived
                // from the work date so it stays deterministic.
                CreatedAtUtc: workDate.ToDateTime(new TimeOnly(17, 0)).AddDays(random.Next(0, 3))));
        }

        return entries;
    }

    /// <summary>Groups matter indices by their client, so entries can be attributed.</summary>
    /// <param name="clientCount">Number of clients.</param>
    /// <param name="matters">Generated matters.</param>
    /// <returns>Matter indices per client index.</returns>
    private static List<List<int>> BuildMatterLookup(int clientCount, List<MatterDraft> matters)
    {
        var lookup = new List<List<int>>(clientCount);
        for (var i = 0; i < clientCount; i++)
        {
            lookup.Add([]);
        }

        for (var i = 0; i < matters.Count; i++)
        {
            lookup[matters[i].ClientIndex].Add(i);
        }

        return lookup;
    }

    /// <summary>Finds the client whose cumulative weight band contains the draw.</summary>
    /// <param name="cumulative">Cumulative weights, ascending, ending at 1.</param>
    /// <param name="draw">A value in [0, 1).</param>
    /// <returns>The selected client index.</returns>
    private static int PickWeighted(double[] cumulative, double draw)
    {
        var index = Array.BinarySearch(cumulative, draw);
        return index >= 0 ? index : Math.Min(~index, cumulative.Length - 1);
    }

    /// <summary>
    /// Picks a work date, resampling until the weekend share is respected.
    /// </summary>
    /// <remarks>
    /// Law firms bill mostly on weekdays. Drawing dates uniformly would put two sevenths of
    /// the dataset on weekends, which produces uniform query plans and a weekly rollup no
    /// reader would believe (constitution P9, FR-013).
    /// </remarks>
    /// <param name="options">Generation inputs.</param>
    /// <param name="random">The single seeded generator.</param>
    /// <param name="totalDays">Days between the earliest date and the reference date.</param>
    /// <returns>A date no later than the reference date.</returns>
    private static DateOnly PickWorkDate(SeedOptions options, Random random, int totalDays)
    {
        var wantWeekend = random.NextDouble() < options.WeekendShare;

        // Bounded rather than unbounded: at most a handful of redraws, then accept what we
        // have. An unbounded loop here would be a hang waiting for a bad seed.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = options.EarliestDate.AddDays(random.Next(totalDays + 1));
            var isWeekend = candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            if (isWeekend == wantWeekend)
            {
                return candidate;
            }
        }

        return options.EarliestDate.AddDays(random.Next(totalDays + 1));
    }
}
