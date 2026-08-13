namespace LexTime.Application.Reporting;

/// <summary>
/// One week of one client's activity, as the report returns it.
/// </summary>
/// <remarks>
/// Not a domain entity and never tracked: it has no key, is never written, and exists only as
/// the shape of a reporting result. The last three properties have no meaning in isolation —
/// they describe this row's relationship to other rows, and they are the part of the feature
/// the hand-computed fixture is aimed at (constitution P12).
/// </remarks>
/// <param name="IsoYear">
/// The week-numbering year: the year containing this week's Thursday. Differs from the
/// calendar year of some of the week's dates at every year boundary — the week beginning
/// Monday 2025-12-29 is ISO year 2026.
/// </param>
/// <param name="IsoWeek">ISO-8601 week number, 1 to 53. Some years have a week 53.</param>
/// <param name="WeekStartDate">The Monday the week begins on.</param>
/// <param name="ClientId">The client these figures belong to.</param>
/// <param name="ClientCode">
/// Carried so a consumer can label the row without a second lookup, which is the whole reason
/// the report joins to the client at all.
/// </param>
/// <param name="ClientName">Carried for the same reason as <paramref name="ClientCode"/>.</param>
/// <param name="BillableHours">
/// Hours recorded against billable entries this week. Derived from stored minutes; always a
/// multiple of 0.1 because durations are multiples of six minutes.
/// </param>
/// <param name="NonBillableHours">
/// Hours recorded against non-billable entries. Reported alongside the billable figure and
/// never netted against it — work that was done but not charged is still work that was done.
/// </param>
/// <param name="BillableAmount">
/// Money billed this week, from billable entries only, each at the rate snapshotted onto it
/// when it was created. Never the timekeeper's current rate: a report that used the current
/// rate would quietly rewrite history every time someone got a raise.
/// </param>
/// <param name="CumulativeBillableHours">
/// The client's running billable total from the first reported week through this one.
/// Confined to the requested range — it does not reach back to activity before it.
/// </param>
/// <param name="HoursDeltaVsPriorWeek">
/// The change in billable hours against the <em>immediately preceding calendar week</em>, not
/// against this client's previous row.
/// <para>
/// <c>null</c> means the preceding week falls outside the requested range, so no comparison
/// was possible. It does <strong>not</strong> mean zero: a week the client was silent through,
/// inside the range, counts as zero billable hours and yields this week's hours in full.
/// Coalescing this to zero misreports the first week of every range.
/// </para>
/// </param>
/// <param name="ClientRankInWeek">
/// Where this client stood among all clients active that week, by billable hours, highest
/// first. Dense: clients tied on hours share a position and the next client takes the one
/// immediately after, not the one after that. Computed across every client regardless of any
/// single-client filter, so a filtered report still shows a true standing rather than 1 of 1.
/// </param>
public sealed record WeeklyBillableRollupRow(
    int IsoYear,
    int IsoWeek,
    DateOnly WeekStartDate,
    int ClientId,
    string ClientCode,
    string ClientName,
    decimal BillableHours,
    decimal NonBillableHours,
    decimal BillableAmount,
    decimal CumulativeBillableHours,
    decimal? HoursDeltaVsPriorWeek,
    int ClientRankInWeek);
