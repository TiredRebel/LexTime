# Contract — `dbo.usp_WeeklyBillableRollup`

The report itself. Everything the endpoint returns is produced here; the application adds an
envelope and nothing else (FR-014).

**File**: `db/programmability/usp_WeeklyBillableRollup.sql`
**Applied by**: the existing `apply-procedures` bootstrap step, never by a migration (P7)
**Invoked by**: `LexTime.Infrastructure.Reporting.SqlWeeklyBillableRollupReader`, with
`CommandType.StoredProcedure` and typed parameters (R5)

This contract is directly callable — a reviewer with `sqlcmd` and a seeded database can
exercise it without the application running. SC-009 requires that, and it is also the form the
hand-computed tests take (R7).

## Parameters

| Parameter | Type | Required | Meaning |
| --- | --- | --- | --- |
| `@FromDate` | `date` | yes | Inclusive lower bound on `WorkDate` |
| `@ToDate` | `date` | yes | Inclusive upper bound on `WorkDate` |
| `@ClientId` | `int` | no, defaults `NULL` | `NULL` returns every client. A value restricts the rows returned and changes no figure within a returned row (FR-012) |

No parameter is optional in the sense of being inferred. `@FromDate` and `@ToDate` have no
defaults; the procedure is not callable without a range.

**The procedure does not validate `@FromDate <= @ToDate`.** An inverted range matches no rows
and returns an empty result, which is coherent. The refusal in FR-018 is the endpoint's job,
where an error can carry a message a caller can act on.

## Result set

One row per `(client, week)` with at least one time entry in range, in this column order.

| # | Column | SQL type | Null? | Notes |
| --- | --- | --- | --- | --- |
| 1 | `IsoYear` | `int` | no | Week-numbering year — the year of this week's Thursday, not necessarily the calendar year of its dates |
| 2 | `IsoWeek` | `int` | no | 1–53 |
| 3 | `WeekStartDate` | `date` | no | The Monday |
| 4 | `ClientId` | `int` | no | |
| 5 | `ClientCode` | `nvarchar(20)` | no | |
| 6 | `ClientName` | `nvarchar(200)` | no | From `Clients.Name` |
| 7 | `BillableHours` | `decimal(12,2)` | no | |
| 8 | `NonBillableHours` | `decimal(12,2)` | no | |
| 9 | `BillableAmount` | `decimal(14,2)` | no | Billable entries only, at each entry's snapshotted rate |
| 10 | `CumulativeBillableHours` | `decimal(12,2)` | no | Running total for the client within the requested range |
| 11 | `HoursDeltaVsPriorWeek` | `decimal(12,2)` | **yes** | See below |
| 12 | `ClientRankInWeek` | `int` | no | Dense rank, ties shared |

### `HoursDeltaVsPriorWeek` — the one nullable column

The null is meaningful and must not be coalesced away by the reader.

| Situation | Value |
| --- | --- |
| The preceding calendar week falls **outside** the requested range | `NULL` |
| The client has a row in the preceding calendar week | this week's billable hours minus that week's |
| The preceding calendar week is **inside** the range and the client has no row in it | this week's billable hours in full — the client billed nothing last week |

The second and third cases are what FR-022's gap fixture must tell apart, and they only differ
if the week before the gap has a different non-zero total from the returning week.

Note the first case is about the *range*, not about the client. A client whose first activity
falls in week 5 of a ten-week range gets a number, not a null: weeks 1–4 are visible and the
client was simply silent.

## Ordering

`ORDER BY WeekIndex, ClientRankInWeek, ClientCode` — chronological, then busiest client first
within the week, then by code to break rank ties. `ClientCode` is unique, so the order is
total and repeatable (FR-013, SC-003).

Callers may rely on this order. It is part of the contract, not an accident of the plan.

## Guarantees

- **Single result set, single round trip.** No output parameters, no return value carrying
  meaning, no second select.
- **Read-only.** No write, no temp table, no `SET` outside `SET NOCOUNT ON`.
- **Idempotent to apply.** `CREATE OR ALTER`, one statement, no `GO` (R3).
- **Session-independent.** No dependence on `SET DATEFIRST` or `SET LANGUAGE`; week identity
  comes from date arithmetic anchored on a known Monday (R1).
- **Empty is success.** A range with no matching activity returns zero rows, not an error.
  Window functions over an empty set produce nothing, which is the correct answer.

## Window functions

Named because P10 requires this procedure to demonstrate them and because a reviewer will look
for exactly these three.

| Function | Over | Produces |
| --- | --- | --- |
| `SUM() OVER (PARTITION BY ClientId ORDER BY WeekIndex ROWS UNBOUNDED PRECEDING)` | each client's weeks in order | `CumulativeBillableHours` |
| `LAG()` × 2, same partition and order | previous week index and previous hours | inputs to `HoursDeltaVsPriorWeek` |
| `DENSE_RANK() OVER (PARTITION BY WeekIndex ORDER BY BillableHours DESC)` | all clients in a week | `ClientRankInWeek` |

Two details are load-bearing and are commented in the file itself (P10):

- **`ROWS UNBOUNDED PRECEDING`, not the default `RANGE`.** Here they give the same answer,
  because `WeekIndex` is unique within a client partition and there are no peers to accumulate
  together. `ROWS` is stated explicitly so the frame is not something a reader has to know the
  default of, and so it stays correct if the grain ever changes.
- **`DENSE_RANK` is computed before `@ClientId` is applied.** The filter lives in the outer
  select. Ranking after filtering would make every single-client request report rank 1
  (FR-012).
