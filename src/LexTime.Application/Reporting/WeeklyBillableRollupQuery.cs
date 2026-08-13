namespace LexTime.Application.Reporting;

/// <summary>
/// A request for the weekly billable rollup, after validation.
/// </summary>
/// <remarks>
/// Bounded on both sides. There is deliberately no open-ended form: a report that picked its
/// own range when one was missing would answer a question nobody asked, and the caller would
/// have no way to tell from the response that it had happened.
/// </remarks>
/// <param name="From">
/// First billing date to include, inclusive. Need not be a Monday — a range starting midweek
/// reports that week with its in-range days only, rather than silently widening to cover the
/// whole week.
/// </param>
/// <param name="To">Last billing date to include, inclusive.</param>
/// <param name="ClientId">
/// Optional single-client filter. Restricts which rows come back and changes no figure inside
/// a row that does — in particular the standing stays the client's position among all clients
/// active that week.
/// </param>
public sealed record WeeklyBillableRollupQuery(DateOnly From, DateOnly To, int? ClientId);
