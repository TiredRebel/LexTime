# Feature Specification: Weekly Billable Rollup

**Feature Branch**: `003-weekly-billable-rollup`

**Created**: 2026-08-13

**Status**: Draft

**Input**: The headline reporting path named in `docs/prd.md` §1, §2.1 Reporting, §4 and
constitution P10. Covers the weekly billable rollup report end to end: the source-controlled
report definition in the database, the read path that invokes it, the endpoint that exposes
it, and the tests that prove its arithmetic. Scoped to one evening per constitution P3 by
following the split `docs/prd.md` §7 already makes: the covering index and the before/after
performance measurement are a separate feature and are **not** in this spec.

## Clarifications

### Session 2026-08-13

- Q: When a client bills in one week, logs nothing for several weeks, then bills again,
  what is the later week's change measured against? → A: The immediately preceding calendar
  week, with a silent week counted as zero billable hours. A client returning after a
  three-week gap therefore shows the whole of the new week's hours as the change. Rows are
  still emitted only for weeks with activity — the report detects that the preceding row is
  not the preceding week rather than filling the gap with zero rows. The alternative of
  comparing against the client's previous row would report a week-on-week change that had
  silently skipped three weeks, which is the class of self-consistent error constitution
  P12 exists to catch; the alternative of emitting a row per client per week would inflate
  the response to 60 × 104 rows and collapse the standing column into a mass tie at zero.
- Q: Does the rollup include clients that are inactive now but billed during the reported
  period? → A: Yes. Recorded because feature 002's Dependencies section required this spec
  to answer it explicitly. Deactivation is forward-looking; a report on a past period
  describes what was billed then. See FR-010.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reviewer reads a firm's billing week by week (Priority: P1)

Someone evaluating this repository has a seeded database and a token. They request the
rollup for a date range and receive, for every week in that range and every client who
logged time in it, the billable and non-billable hours, the money billed, that client's
running total, how the week compared with the one before, and where the client stood
against the other clients that week.

**Why this priority**: This is the artifact the repository exists to show
(`docs/prd.md` §1, constitution P10). Everything already built — schema, seed, auth,
bootstrap — exists to make this one response possible and believable.

**Independent Test**: With a seeded database, request the report over the full seeded range
and inspect the returned rows. Delivers value alone: the reporting path is complete and
demonstrable without any CRUD endpoint existing.

**Acceptance Scenarios**:

1. **Given** a seeded database, **When** the report is requested for a range covering the
   whole seeded history, **Then** it returns one row per week per client that logged time,
   carrying all the figures above.
2. **Given** a client who logged time in a week, **When** that week's row is read, **Then**
   billable and non-billable hours are reported separately and the billed amount reflects
   only the billable portion.
3. **Given** a client with activity in several weeks of the range, **When** their rows are
   read in week order, **Then** the running billable total increases monotonically and the
   final week's running total equals the sum of that client's billable hours across the
   range.
4. **Given** a week in which several clients logged billable time, **When** that week's
   rows are read, **Then** each client carries its standing for that week, the busiest
   client standing first.
5. **Given** a request narrowed to a single client, **When** the rows are read, **Then**
   only that client's weeks are returned and its standing still reflects its true position
   among all clients active in those weeks.

---

### User Story 2 - The numbers can be trusted without trusting the report (Priority: P2)

A reader who does not take the report's word for it can check it. Every derived figure —
the running total, the week-on-week change, the standing — is verified against a small
dataset whose expected answers were worked out by hand, including the weeks where this
class of calculation usually goes wrong.

**Why this priority**: Constitution P12 and P15. A report that computes running totals and
rankings is self-consistent when it is wrong; the only thing that catches it is an
expectation derived independently of the report. It is P2 rather than P1 because the report
must exist before it can be checked, but the feature is not deliverable without it.

**Independent Test**: Load a small fixture with known content, run the report over it, and
compare every field of every row against values computed by hand and written into the test
before the report was run.

**Acceptance Scenarios**:

1. **Given** a hand-computed fixture, **When** the report runs over it, **Then** every
   figure in every row matches the hand-computed expectation exactly.
2. **Given** a week in which a client logged only non-billable time, **When** the report
   runs, **Then** the client appears with zero billable hours, zero billed amount, and its
   non-billable hours intact.
3. **Given** a range containing no time entries at all, **When** the report runs, **Then**
   it returns no rows and reports success.
4. **Given** a client who bills, then logs nothing for several weeks, then bills again,
   **When** the returning week's row is read, **Then** its change equals that week's whole
   billable hours — not the difference against the week it last billed in — and the silent
   weeks produce no rows.
5. **Given** a client's first week of activity in the range, **When** that row is read,
   **Then** the week-on-week change is reported as no prior week rather than as a change
   from zero.
6. **Given** a range whose end falls in the first days of January, **When** the rows are
   read, **Then** each week is attributed to the week-numbering year it belongs to, not to
   the calendar year of its dates.

---

### User Story 3 - A malformed or unauthorised request fails clearly (Priority: P3)

A caller who omits a date, inverts the range, or arrives without credentials gets a
specific, machine-readable refusal rather than an empty result, a stack trace, or a
plausible-looking report over a range they did not ask for.

**Why this priority**: The access boundary was established in feature 001 and this endpoint
must not be the hole in it. It is P3 because it protects the feature rather than
constituting it.

**Independent Test**: Issue requests with a missing date, an inverted range, a
non-existent client and no credentials, and inspect each response.

**Acceptance Scenarios**:

1. **Given** no credentials, **When** the report is requested, **Then** the request is
   refused and no report data is returned.
2. **Given** a range whose start is later than its end, **When** the report is requested,
   **Then** it is refused with an error naming the offending values.
3. **Given** a request missing either date, **When** the report is requested, **Then** it
   is refused; no default range is assumed.
4. **Given** a client identifier that matches no client, **When** the report is requested,
   **Then** it returns no rows and reports success — the report is over a period, and a
   client with nothing in that period legitimately produces nothing.

---

### Edge Cases

- **A client who is inactive today but billed during the reported period.** The seed
  guarantees these exist (feature 002 FR-017). Covered by FR-010.
- **A week where a client logged only non-billable time.** The client must still appear;
  omitting them hides work that was done.
- **A client's first week in the range.** There is no prior week to compare against, and
  reporting a change of "the whole week's hours" would be a fabrication.
- **A gap of several weeks in a client's activity.** The returning week is compared against
  the silent week immediately before it, not against the week the client last billed in
  (FR-008). The silent weeks themselves produce no rows.
- **A range that starts or ends mid-week.** Only the in-range days count toward that week's
  figures; the week is reported as the partial week it is.
- **A week spanning a year boundary.** Under ISO-8601 numbering the days of one week always
  belong to a single week-numbering year, which may differ from the calendar year of some
  of its dates. Week 53 exists in some years and not others.
- **A range containing no activity at all.** An empty result, not an error and not a row of
  zeros.
- **Every client tied on billable hours in a week.** Standing is shared, not arbitrarily
  broken.
- **A client active in the range whose only matters are now inactive.** Included; the report
  describes what was billed, not what may be billed next.

## Requirements *(mandatory)*

### Functional Requirements

**What the report returns**

- **FR-001**: The report MUST accept a start date and an end date, both required and both
  inclusive, and MUST consider only time entries whose billing date falls within them.
- **FR-002**: The report MUST group by week and by client, returning one row per
  combination that has at least one time entry in the range. Combinations with no entries
  MUST NOT produce rows — including the silent weeks that FR-008 compares against, which are
  detected rather than materialised. A row per client per week regardless of activity would
  return roughly 6,200 rows for the seeded range irrespective of how much was billed, and
  would collapse the standing in FR-009 into a mass tie at zero.
- **FR-003**: Weeks MUST be identified by ISO-8601 week numbering — weeks begin on Monday,
  and each week belongs to the week-numbering year containing its Thursday. Each row MUST
  carry the week-numbering year, the week number, and the date of that week's Monday.
- **FR-004**: Each row MUST report billable and non-billable hours separately, derived from
  recorded minutes.
- **FR-005**: Each row MUST report the amount billed, computed only from billable entries
  using the rate snapshotted on each entry. Non-billable entries MUST contribute nothing to
  the amount. The timekeeper's current rate MUST NOT be used.
- **FR-006**: Each row MUST identify its client by internal identifier, client code and
  name, so the response is readable without a second lookup.
- **FR-007**: Each row MUST report the client's running total of billable hours from the
  first reported week through that week. The running total MUST be confined to the
  requested range and MUST NOT include activity before it.
- **FR-008**: Each row MUST report the change in the client's billable hours against the
  **immediately preceding calendar week**. Where the client logged nothing in that week, the
  comparison MUST treat it as zero billable hours, so the reported change is the whole of
  the current week's billable hours. The report MUST NOT compare against the client's
  previous *row* when that row is not the previous week.

  Where the immediately preceding calendar week falls **outside the requested range**, the
  change MUST be reported as *absent* rather than as a change from zero. A week the report
  cannot see is not a week in which nothing was billed, and conflating the two would make
  every client's first row overstate its change by the whole of that week.
- **FR-009**: Each row MUST report the client's standing among clients that week by
  billable hours, highest first. Clients tied on billable hours MUST share a standing, and
  a shared standing MUST NOT consume the positions below it — two clients tied at the top
  are both first and the next is second.

**Which rows appear**

- **FR-010**: A client that is inactive today MUST still appear for the weeks in which it
  had activity. Deactivation is forward-looking; a report on a past period describes what
  was billed then. Feature 002 FR-017 preserves the history that makes this testable, and
  this requirement is the position that feature's Dependencies section required this spec
  to state.
- **FR-011**: Entries against inactive matters and inactive timekeepers MUST likewise be
  included on the same reasoning.
- **FR-012**: The report MUST accept an optional single-client filter. The filter MUST
  restrict which rows are returned, and MUST NOT change any figure within a returned row —
  in particular, the client's standing MUST remain its position among all clients active in
  that week, not a position of one out of one.
- **FR-013**: Row order MUST be deterministic and repeatable: identical requests MUST return
  identical rows in an identical order.

**How it is computed**

- **FR-014**: The grouping, the running total, the week-on-week change and the standing MUST
  all be computed by the database and returned already computed. Application code MUST NOT
  iterate rows to derive any of them. *This is what makes the report the artifact it claims
  to be; computing it in application code would make the database incidental.*
- **FR-015**: The report definition MUST live in a source-controlled file applied by the
  existing bootstrap step, MUST be safe to re-apply without being dropped first, and MUST
  NOT be created by a schema migration (constitution P7).
- **FR-016**: The report MUST be readable, with a comment wherever the window over which a
  figure is accumulated is not obvious from the expression itself (constitution P10).

**Access and failure**

- **FR-017**: The report endpoint MUST require authentication and MUST return no report data
  to an unauthenticated caller.
- **FR-018**: A start date later than the end date MUST be refused with an error naming both
  values. A missing date MUST be refused; no default range may be assumed.
- **FR-019**: Errors MUST use the same machine-readable problem format as the rest of the
  API.
- **FR-020**: A range with no matching activity, and a client filter matching no client,
  MUST each return an empty result and report success. Neither is an error.

**Verification**

- **FR-021**: A fixture small enough to compute by hand MUST exist, and the expected running
  totals, week-on-week changes and standings MUST be written into the test from that hand
  computation — never captured from a run of the report (constitution P12).
- **FR-022**: The fixture MUST include a week with only non-billable activity, a client with
  a multi-week gap, a client's first week in the range, a tie on billable hours, and a range
  boundary that falls mid-week. The gap case MUST assert both readings apart: that the
  returning week's change is its own hours, and that it is *not* the difference against the
  week the client last billed in. A fixture where those two happen to coincide does not
  test FR-008.
- **FR-023**: An empty range MUST be covered by its own test. Reporting calculations that
  accumulate across rows commonly fail on the empty case in a way no populated test detects.

### Key Entities

- **Reporting period**: the requested start and end dates, inclusive, plus an optional
  client filter. Bounded on both sides — there is no open-ended report.
- **Rollup row**: one week for one client. Carries the week's identity (week-numbering year,
  week number, Monday date), the client's identity (identifier, code, name), the week's own
  figures (billable hours, non-billable hours, amount billed), and three figures that only
  have meaning relative to other rows (running total, change against the prior week,
  standing within the week).
- **Time entry** *(existing)*: the source of every figure. Contributes its minutes, its
  billable flag and its snapshotted rate; its billing date determines which week it falls in.
- **Client, Matter, Timekeeper** *(existing)*: entries reach a client through their matter.
  Current active status affects none of them for this report (FR-010, FR-011).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A reviewer with a seeded database and the token the bootstrap printed obtains
  a complete rollup over the full 24 months of seeded history in a single request, with no
  paging and no follow-up call.
- **SC-002**: Every figure in every row of the hand-computed fixture matches its
  independently derived expectation — zero discrepancies across running totals, week-on-week
  changes and standings.
- **SC-003**: Two identical requests return identical rows in identical order, byte for
  byte.
- **SC-004**: The full-range report over the seeded dataset is produced by a single database
  round trip and returns at most roughly 6,500 rows — 60 clients across 104 weeks, less the
  combinations with no activity.
- **SC-005**: An empty range and a zero-billable week each return a correct result, verified
  by tests that exist independently of the populated case.
- **SC-006**: An unauthenticated request obtains no figure from the report under any
  parameter combination.
- **SC-007**: At least one client that is inactive today appears in the report for a period
  during which it was billed, confirmed against the seeded dataset.
- **SC-008**: Every rejected request names what was wrong with it, as judged by someone who
  has not read the code.

## Assumptions

- **The range is inclusive at both ends and weeks at its edges are partial.** Entries are
  selected by billing date within the range; a week that the range only partly covers is
  reported with the in-range days only. Silently widening the range to whole weeks would
  return figures the caller did not ask for.
- **Hours are derived from recorded minutes.** Minutes are the stored unit
  (`docs/prd.md` §3); hours are a presentation of them and carry enough decimal places that
  six-minute increments are exactly representable.
- **The rate is the one snapshotted on the entry.** PRD §2.1 rule 6 already fixes this;
  restated here because a report that joined to the timekeeper's current rate would look
  correct and quietly rewrite history.
- **Standing is computed before the client filter is applied.** A standing of "1 of 1" is
  information-free; the useful reading of a single-client report is where that client stood
  among all of them.
- **No covering index is added by this feature.** The schema ships with the day-one index
  set on purpose (`docs/prd.md` §3). Adding the index here would leave the later before/after
  comparison with no honest "before" to measure, so the report is built and tested against
  the un-indexed schema.
- **The report is exposed as one endpoint over the existing API surface**, reusing the
  authentication and error format established in feature 001. No new access mechanism.
- **Where the read path and its interface live is a planning decision.** Constitution P5
  fixes that reporting reads go through the procedure directly rather than the ORM's entity
  model, and P4 fixes the layering; the concrete placement belongs in `plan.md`.

## Out of Scope

Named here so the boundary reads as chosen rather than missed.

| Not in this feature | Where it belongs |
| --- | --- |
| The covering index and the before/after execution plans, logical reads and elapsed times | The next feature. `docs/prd.md` §7 puts the procedure and its call path on evening 2 and the measurement on evening 3; constitution P3 caps one spec at one evening |
| CRUD endpoints for clients, matters and time entries | A later feature. The report reads what the seed wrote and needs none of them |
| Enforcement of the six domain rules in application code | Same. Only the storage-level duration constraints from feature 001 apply to this feature's data |
| Any second report | `docs/prd.md` §2.2 — the pattern is proven once |
| Caching, pagination or rate limiting of the report | `docs/prd.md` §2.2 — there is no load to justify them, and SC-004 bounds the response instead |

## Dependencies

- **Blocked by**: feature 001 for the schema, the access boundary and the error format;
  feature 002 for the procedure-application step, the seeded dataset, and the fixed
  reference date any figure quoted from the seed must be read against.
- **Blocks**: the performance feature, which measures this report before and after adding
  the covering index and cannot begin until there is a report to measure. That feature owns
  `docs/performance.md`, including citing feature 002's reference date alongside its numbers.
- **External**: none beyond what features 001 and 002 already require.
