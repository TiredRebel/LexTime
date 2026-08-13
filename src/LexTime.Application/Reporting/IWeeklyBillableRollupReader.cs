namespace LexTime.Application.Reporting;

/// <summary>
/// Reads the weekly billable rollup from the database.
/// </summary>
/// <remarks>
/// Declared here and implemented in <c>LexTime.Infrastructure</c>. This is the boundary
/// constitution P5 requires: reporting reads go through a stored procedure invoked directly,
/// and the implementing type is the only place raw ADO.NET appears in the solution. The
/// application layer states what it needs and stays ignorant of how the rows arrive.
/// <para>
/// One implementation, and no plans for a second. P4 makes that the expected shape here rather
/// than something to defer until a second one turns up.
/// </para>
/// </remarks>
public interface IWeeklyBillableRollupReader
{
    /// <summary>
    /// Executes the rollup for a range and returns its rows already computed.
    /// </summary>
    /// <remarks>
    /// Every figure — including the running total, the change against the prior week and the
    /// within-week standing — arrives finished. Implementations must not iterate the result to
    /// derive any of them; that calculation belongs to the database and moving it here would
    /// make the database incidental to a feature that exists to show it doing the work.
    /// </remarks>
    /// <param name="query">The range and optional client filter to report on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The rows, chronological and busiest client first within each week. Empty when the range
    /// contains no activity — an empty report is a result, not an error.
    /// </returns>
    Task<IReadOnlyList<WeeklyBillableRollupRow>> ReadAsync(
        WeeklyBillableRollupQuery query,
        CancellationToken cancellationToken = default);
}
