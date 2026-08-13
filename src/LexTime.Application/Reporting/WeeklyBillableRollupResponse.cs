namespace LexTime.Application.Reporting;

/// <summary>
/// The rollup, with the range it was computed over.
/// </summary>
/// <remarks>
/// The range is echoed back so a stored or forwarded response says what it is a report of.
/// Rows carry a week and a client but nothing that identifies the request they came from, and
/// a saved payload without its range is a set of numbers nobody can check.
/// </remarks>
/// <param name="From">The requested start date, as supplied.</param>
/// <param name="To">The requested end date, as supplied.</param>
/// <param name="Rows">
/// One entry per week per client with activity in the range, chronological and busiest client
/// first within a week. Empty — never null — when nothing matched: a range with no activity
/// and a client filter that matches nobody are both correct answers, not failures.
/// </param>
public sealed record WeeklyBillableRollupResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<WeeklyBillableRollupRow> Rows);
