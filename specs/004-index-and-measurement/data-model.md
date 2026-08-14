# Data Model — Index and Measured Performance (004)

Two kinds of thing here, and they are worth keeping apart: **one schema change**, which is
persistent and shipped, and **a set of measurement records**, which exist for the length of a
run and end up as committed documents rather than as rows in a table.

Nothing is stored in the database by this feature. No table, no column, no row.

## The schema change

### `IX_TimeEntries_WorkDate_Billable`

A non-clustered index on `dbo.TimeEntries`, exactly as `docs/prd.md` §3 commits to.

| Part | Columns | Why |
| --- | --- | --- |
| Key | `WorkDate`, `IsBillable` | The report's `WHERE` filters on a `WorkDate` range and every figure it produces splits on `IsBillable` |
| Included | `MatterId`, `DurationMinutes`, `HourlyRateSnapshot` | The only other columns the report reads from this table. Carrying them makes the index covering — the query can be answered without returning to the clustered index for each row |

`MatterId` is the join to the client; `DurationMinutes` feeds both hour totals; `HourlyRateSnapshot`
feeds the amount. `UserId` and `Narrative` are deliberately not included: the report does not
read them, and every included column costs space on every write.

**Declared in `TimeEntryConfiguration` and applied by a migration** (R1), so a fresh clone gets
it. It is a table structure — constitution P7 keeps procedures out of migrations, not indexes.

**What does not change**: no key, no constraint, no column type, no relationship. The check
constraint on `DurationMinutes` and the deliberate absence of any constraint on `WorkDate` both
stand exactly as feature 001 left them.

## Measurement records

In-memory during a run, then serialised into `docs/performance.md` and its companion files.
None of these is persisted to the database.

### `IndexState`

Which of the two conditions the database is in. Two values, and nothing between them.

| Value | Meaning |
| --- | --- |
| `WithoutIndex` | The covering index has been dropped for the duration of the reading |
| `WithIndex` | The committed state, and where the database is left however the run ends (FR-014) |

### `RequestShape`

Which call is being measured. Two values (R9).

| Value | Parameters |
| --- | --- |
| `FullRange` | `2024-08-13` to `2026-08-13`, no client filter |
| `SingleClient` | The same dates, filtered to the busiest client |

### `MeasurementReading`

One captured run of one combination.

| Field | Notes |
| --- | --- |
| `IndexState` | |
| `RequestShape` | |
| `LogicalReads` | Summed across the tables the statistics output names. Deterministic — every reading of a combination produces the same figure, which is why FR-016 can require repeat runs to agree |
| `ElapsedMilliseconds` | From `SET STATISTICS TIME`. Varies between readings and between machines; this is the figure FR-012 requires be reported as secondary |
| `RowCount` | How many rows the call returned. Equal across index states by FR-003, and cheap insurance that a reading was taken against the query it claims |
| `ResultHash` | A hash of the full result set. The two index states' hashes must match — this is SC-001's full-scale equivalence proof (R8) |
| `RawStatistics` | The verbatim `STATISTICS IO`/`TIME` text. Committed as-is, so the summary table can be audited against its source rather than trusted (R3) |
| `PlanXml` | The actual execution plan, with runtime counters. Written to a `.sqlplan` a reviewer can open |

### `MeasuredCombination`

The five readings of one combination, reduced for publication.

| Field | Derivation |
| --- | --- |
| `LogicalReads` | One figure. All five readings agree; if they ever did not, that is a defect and not something to average away |
| `ElapsedMedian`, `ElapsedMin`, `ElapsedMax` | Median and range across the five readings (FR-011) |
| `PlanFile` | Path to the committed `.sqlplan` |
| `StatisticsFile` | Path to the committed raw output |

Four of these exist per run. They are what the summary table in `docs/performance.md` is built
from.

## Existing entities, and how this feature touches them

### `TimeEntries` — indexed, otherwise untouched

The index changes how rows are reached, not what they contain and not what the report returns
(FR-003). No row is written, updated or deleted by this feature.

### The seed dataset — fixed, and the reason any of this is comparable

400,000 entries, reference date 2026-08-13, random seed 20260813, all committed constants. Two
runs a week apart on different machines read the same rows in the same order.

**FR-018 makes this immutable for the purposes of this feature.** If the index turns out to help
less than hoped, the number is published as it is; the dataset is not enlarged to improve it.
Changing it would invalidate feature 002's row-count criterion, its verification bands, and every
test asserting those volumes — a large cost, paid to make one figure look better.

### `dbo.usp_WeeklyBillableRollup` — not modified

The procedure is measured, not improved. A query change in the same feature would make it
impossible to say whether the index or the rewrite caused the difference, which is why the spec
puts it out of scope.
