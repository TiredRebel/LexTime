namespace LexTime.Application.TimeEntries;

/// <summary>
/// A request to record new time.
/// </summary>
/// <remarks>
/// <b>There is no rate field, deliberately.</b> The rate is captured from the timekeeper, never
/// supplied — a caller able to state it could bill at any figure they liked, and rule 6 would be
/// decoration rather than a rule.
/// </remarks>
/// <param name="UserId">The timekeeper recording the work.</param>
/// <param name="MatterId">The matter it was carried out for.</param>
/// <param name="WorkDate">The billing date. Rule 4 constrains it.</param>
/// <param name="DurationMinutes">Duration in whole minutes. Rules 1, 2 and 3 constrain it.</param>
/// <param name="IsBillable">Whether the time is charged.</param>
/// <param name="Narrative">Description of the work.</param>
public sealed record RecordTimeEntryCommand(
    int UserId,
    int MatterId,
    DateOnly WorkDate,
    int DurationMinutes,
    bool IsBillable,
    string Narrative);

/// <summary>
/// A request to correct an existing entry.
/// </summary>
/// <remarks>
/// Carries the whole entry minus the two fields that are not revisable, so "unchanged" is a
/// comparison against what is stored rather than an absence the server has to interpret.
/// <para>
/// <b>No timekeeper</b>: moving an entry between people would change whose daily total it counts
/// against and whose rate it should have captured, and neither has a defined answer. Re-record it
/// instead. <b>No rate</b>: that is rule 6, and leaving the field out means the mistake cannot be
/// made through the API at all.
/// </para>
/// </remarks>
/// <param name="MatterId">The matter. Rule 5 applies only if this differs from what is stored.</param>
/// <param name="WorkDate">The billing date. Rule 4 applies only if this differs from what is stored.</param>
/// <param name="DurationMinutes">Duration in whole minutes. Rules 1, 2 and 3 always apply.</param>
/// <param name="IsBillable">Whether the time is charged.</param>
/// <param name="Narrative">Description of the work.</param>
public sealed record ReviseTimeEntryCommand(
    int MatterId,
    DateOnly WorkDate,
    int DurationMinutes,
    bool IsBillable,
    string Narrative);

/// <summary>
/// Filters and a page window for listing entries.
/// </summary>
/// <param name="UserId">Restrict to one timekeeper, or null for all.</param>
/// <param name="MatterId">Restrict to one matter, or null for all.</param>
/// <param name="From">Inclusive lower bound on work date, or null for none.</param>
/// <param name="To">Inclusive upper bound on work date, or null for none.</param>
/// <param name="Skip">How many matching entries to pass over. Negative values are treated as zero.</param>
/// <param name="Take">
/// How many to return. Bounded rather than honoured literally — an unfiltered request must not
/// be able to ask for the whole table.
/// </param>
public sealed record ListTimeEntriesQuery(
    int? UserId,
    int? MatterId,
    DateOnly? From,
    DateOnly? To,
    int Skip,
    int Take)
{
    /// <summary>Page size used when the caller asks for none.</summary>
    public const int DefaultTake = 50;

    /// <summary>Largest page this API will return, whatever the caller asks for.</summary>
    public const int MaximumTake = 200;

    /// <summary>Clamps the page window into its permitted range.</summary>
    /// <remarks>
    /// Applied in the handler rather than at the endpoint, so every caller of the use case gets
    /// the same bounds — including a future one that is not an HTTP request.
    /// </remarks>
    /// <returns>The same query with a sane skip and take.</returns>
    public ListTimeEntriesQuery Clamped() => this with
    {
        Skip = Math.Max(0, this.Skip),
        Take = this.Take is <= 0 ? DefaultTake : Math.Min(this.Take, MaximumTake),
    };
}

/// <summary>One page of entries, with the total that matched the filters.</summary>
/// <param name="Skip">The window's offset, after clamping.</param>
/// <param name="Take">The window's size, after clamping.</param>
/// <param name="Total">
/// How many entries match the filters in total — not how many are in this page. A caller cannot
/// page sensibly without it.
/// </param>
/// <param name="Items">The page. Empty for a range with nothing in it, which is a result rather than an error.</param>
public sealed record TimeEntryPage(
    int Skip,
    int Take,
    int Total,
    IReadOnlyList<TimeEntryDto> Items);
