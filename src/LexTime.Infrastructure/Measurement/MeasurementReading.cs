namespace LexTime.Infrastructure.Measurement;

/// <summary>Which of the two conditions the database is in for a reading.</summary>
public enum IndexState
{
    /// <summary>
    /// The covering index has been dropped for the length of the reading. Manufactured by the
    /// measurement and never a state the repository is left in.
    /// </summary>
    WithoutIndex,

    /// <summary>The committed state, and where the database is left however a run ends.</summary>
    WithIndex,
}

/// <summary>Which call is being measured.</summary>
public enum RequestShape
{
    /// <summary>The full seeded window across every client.</summary>
    FullRange,

    /// <summary>
    /// The same window narrowed to the busiest client.
    /// </summary>
    /// <remarks>
    /// Measured separately rather than assumed to behave like the unfiltered call. The report
    /// ranks every client before narrowing to one, so this path does the full-population
    /// aggregation and then discards most of the result — whether the index helps it more or
    /// less is not deducible from the other measurement.
    /// </remarks>
    SingleClient,
}

/// <summary>
/// One captured run of one combination.
/// </summary>
/// <param name="State">Which index state the reading was taken in.</param>
/// <param name="Shape">Which call was measured.</param>
/// <param name="LogicalReads">
/// Total logical reads across every table the statistics output named.
/// <b>Deterministic</b>: a property of the plan, identical on every machine and every run. This
/// is the figure the published claim rests on, and the one a reviewer's own run must match.
/// </param>
/// <param name="ElapsedMilliseconds">
/// Elapsed time as the server reported it.
/// <b>Not deterministic</b>: a property of the hardware, the other load on it, and what the
/// buffer pool held. Published as a median with its range, and weighted below the read count
/// for exactly this reason.
/// </param>
/// <param name="RowCount">
/// Rows the call returned. Equal across index states by requirement, and cheap insurance that a
/// reading was taken against the query it claims to describe.
/// </param>
/// <param name="ResultHash">
/// Hash of the ordered result set. The two index states' hashes must match — this is the
/// full-scale equivalence proof, over all 400,000 entries, which no test can afford to load.
/// </param>
/// <param name="RawStatistics">
/// The verbatim <c>STATISTICS IO</c>/<c>TIME</c> text, exactly as the server sent it. Committed
/// unedited so the published summary can be audited against its source rather than believed.
/// </param>
public sealed record MeasurementReading(
    IndexState State,
    RequestShape Shape,
    long LogicalReads,
    long ElapsedMilliseconds,
    int RowCount,
    string ResultHash,
    string RawStatistics);

/// <summary>
/// Several readings of one combination, reduced for publication.
/// </summary>
/// <param name="State">Which index state.</param>
/// <param name="Shape">Which call.</param>
/// <param name="LogicalReads">
/// One figure, not an average. Every reading agrees; if they ever did not, that is a defect to
/// investigate rather than a spread to smooth over.
/// </param>
/// <param name="ElapsedMedian">Median elapsed time across the readings.</param>
/// <param name="ElapsedMin">Fastest reading.</param>
/// <param name="ElapsedMax">Slowest reading.</param>
/// <param name="RowCount">Rows returned, identical across readings.</param>
/// <param name="ResultHash">Hash of the result set, identical across readings.</param>
/// <param name="PlanXml">The actual execution plan, with runtime counters.</param>
/// <param name="RawStatistics">Verbatim statistics text from the first reading.</param>
public sealed record MeasuredCombination(
    IndexState State,
    RequestShape Shape,
    long LogicalReads,
    long ElapsedMedian,
    long ElapsedMin,
    long ElapsedMax,
    int RowCount,
    string ResultHash,
    string PlanXml,
    string RawStatistics);
