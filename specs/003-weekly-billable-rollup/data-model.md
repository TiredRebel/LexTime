# Data Model — Weekly Billable Rollup (003)

**This feature adds no table, no column and no migration.** The schema is feature 001's and is
unchanged. What follows describes the shape the report *produces* and the read models that
carry it, plus how the existing tables are read.

The absence of a migration is itself a design statement: constitution P7 keeps procedures out
of migrations, and PRD §3 keeps the index set at its day-one minimum on purpose so the next
feature has an honest "before" to measure. A migration appearing in this feature would mean
something has gone wrong.

## Read models

Three records in `LexTime.Application/Reporting/`. All are immutable, all are plain — no base
class, no interface, no attributes beyond what serialisation needs.

### `WeeklyBillableRollupQuery`

The request, after validation. Bounded on both sides; there is no open-ended report (FR-001).

| Field | Type | Notes |
| --- | --- | --- |
| `From` | `DateOnly` | Inclusive. Entries are selected by billing date, so a `From` that is not a Monday yields a partial first week, reported as such |
| `To` | `DateOnly` | Inclusive |
| `ClientId` | `int?` | Optional filter. Restricts which rows are returned and changes no figure inside a returned row (FR-012) |

**Validation** (FR-018, enforced at the endpoint before the handler is called):

- both dates required — no default range is assumed
- `From` must not be later than `To`; the error names both values

### `WeeklyBillableRollupRow`

One week for one client. Eleven fields, mapped one-to-one from the procedure's result set —
see [contracts/usp-weekly-billable-rollup.md](./contracts/usp-weekly-billable-rollup.md) for
the SQL types.

| Field | Type | Meaning |
| --- | --- | --- |
| `IsoYear` | `int` | The week-numbering year — the year containing this week's Thursday. Differs from the calendar year of some of the week's dates at every year boundary (R1) |
| `IsoWeek` | `int` | ISO week number, 1–53 |
| `WeekStartDate` | `DateOnly` | The Monday of the week |
| `ClientId` | `int` | |
| `ClientCode` | `string` | Included so the response is readable without a second lookup (FR-006) |
| `ClientName` | `string` | Same |
| `BillableHours` | `decimal` | |
| `NonBillableHours` | `decimal` | Reported separately, never netted against billable |
| `BillableAmount` | `decimal` | Billable entries only, at the rate snapshotted on each entry |
| `CumulativeBillableHours` | `decimal` | Running total for this client from the first reported week through this one, confined to the requested range (FR-007) |
| `HoursDeltaVsPriorWeek` | `decimal?` | **Nullable, and the null means something.** Absent when the preceding calendar week falls outside the requested range. A silent week *inside* the range is zero, not null (FR-008) |
| `ClientRankInWeek` | `int` | Dense rank by billable hours descending. Ties share a rank and do not consume the positions below (FR-009) |

The last three exist only in relation to other rows. That is what makes them the interesting
part of the feature and the part the hand-computed fixture is aimed at.

### `WeeklyBillableRollupResponse`

The envelope the handler builds.

| Field | Type | Notes |
| --- | --- | --- |
| `From` | `DateOnly` | Echoed back, so a stored or shared response is self-describing |
| `To` | `DateOnly` | Same |
| `Rows` | `IReadOnlyList<WeeklyBillableRollupRow>` | Empty for a range with no activity — an empty list, not null, and not an error (FR-020) |

## Existing entities, as this feature reads them

Read-only. No entity is loaded through EF Core on this path (P5).

### `TimeEntries` — the source of every figure

| Column | Used for |
| --- | --- |
| `WorkDate` | Range filter and week attribution. The *billing* date, not the entry date |
| `DurationMinutes` | Both hour totals. Summed as integers before any conversion (R10) |
| `IsBillable` | Splits the two hour totals and gates the amount |
| `HourlyRateSnapshot` | The amount. **The snapshot, never the timekeeper's current rate** — PRD §2.1 rule 6. Joining to `Users.DefaultHourlyRate` would look correct and quietly rewrite history |
| `MatterId` | The only route from an entry to a client |

`UserId` and `Narrative` are not read. The report is per client, not per timekeeper.

### `Matters` — the join to a client

Contributes `ClientId` only. `IsActive` is **not** filtered on: an entry recorded against a
matter that has since been closed still happened (FR-011).

### `Clients` — identity for the row

Contributes `ClientCode` and `Name`. `IsActive` is **not** filtered on (FR-010) — this is the
position the spec was required to state, and the seed guarantees roughly 10–15% of clients are
inactive with history intact, so it is testable rather than theoretical.

### `Users` — not read

Named here only to be explicit that it is absent, because joining to it is the obvious wrong
turn: it is where the current rate lives.

## Relationships used

```text
TimeEntries ──MatterId──> Matters ──ClientId──> Clients
```

Two inner joins. Both are along foreign keys that exist and are indexed, so neither is where
the interesting cost lives. The aggregation and the sort are.

## Grouping key

The report groups by `(ClientId, WeekIndex)` where
`WeekIndex = DATEDIFF(day, '19000101', WorkDate) / 7` — a day-count ordinal, monotonic across
year boundaries, independent of `SET DATEFIRST`. R1 records why the obvious alternative
(`IsoYear * 100 + IsoWeek`) is wrong every January and right the rest of the time.

`WeekIndex` is an internal artefact and is **not** returned. Callers get the three derived
week fields instead.

## Row volume

One row per `(client, week)` with at least one entry in range. For the full seeded history:
60 clients × 104 weeks = **6,240 upper bound**, and fewer in practice because the seed's
activity is deliberately uneven (P9) and the long-tail clients are silent in many weeks.
SC-004 is the assertion.

Combinations with no activity produce no row. The week-on-week change reaches back into those
silent weeks by detecting them, not by materialising them (R2).
