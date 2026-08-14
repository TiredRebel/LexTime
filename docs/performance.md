# Performance — the covering index, measured

Every figure below came from one run of `dotnet run --project src/LexTime.Api measure` against
the committed seed. Nothing here is estimated, rounded from memory, illustrative, or carried
over from another machine. The raw `SET STATISTICS IO`/`TIME` output each number was read from
is committed beside this file, unedited, together with the reduced summary — so the tables can
be checked rather than believed.

Constitution P8 is the rule this document exists to satisfy: a performance claim is worth
exactly what its measurement is.

---

## Method

Read this before the numbers, so the numbers can be judged rather than discovered to have
caveats afterwards.

**The dataset.** 400,000 time entries across 220 matters, 60 clients and 25 timekeepers,
spanning 2024-08-13 to 2026-08-13. Reference date `2026-08-13`, random seed `20260813`, all
committed constants — which is what makes two runs, a week apart on different machines,
comparable at all.

**What was measured.** `dbo.usp_WeeklyBillableRollup` over the full seeded window, in four
combinations: with and without the covering index, for each of two request shapes.

The second shape is measured separately rather than assumed to behave like the first. The
report ranks every client before narrowing to one, so a single-client request performs the
full-population aggregation and then discards most of the result. Whether the index helps that
path equally is not deducible from the unfiltered measurement — and the answer turns out to be
the most informative thing here.

**Cache convention.** `CHECKPOINT` followed by `DBCC DROPCLEANBUFFERS` before every reading,
applied identically to both index states. Measuring one warm and the other cold would compare
the buffer pool, and whichever ran second would win.

> `DBCC DROPCLEANBUFFERS` clears the buffer pool for the **whole instance**, not one database.
> Harmless against the single-purpose container the quickstart brings up, unwelcome against
> anything shared. The verb says so before it starts.

**Readings.** Five per combination. Logical reads are reported as a single figure because all
five agreed — the measurement fails rather than averages if they ever disagree. Elapsed times
are a median with the full range.

**Why read counts lead and elapsed time follows.** Logical reads are a property of the plan:
identical on every machine, every run. Elapsed time is a property of the hardware, the other
load on it, and what the container had cached. Publishing them with equal authority would
invite a reader to check the one that does not reproduce, find that it does not, and discount
both. **A reviewer running this on their own machine should match the read counts exactly and
should not expect to match the milliseconds.**

**Environment.** SQL Server 2022 Developer Edition, `16.0.4265.3`, in Docker with no memory
limit set. Host: Intel Core i7-13650HX, 64 GB RAM, Windows 11 Pro. .NET SDK 9.0.317.

---

## The index

```sql
CREATE NONCLUSTERED INDEX IX_TimeEntries_WorkDate_Billable
    ON dbo.TimeEntries (WorkDate, IsBillable)
    INCLUDE (MatterId, DurationMinutes, HourlyRateSnapshot);
```

The key columns are what the report filters and splits on — a `WorkDate` range in the `WHERE`,
and `IsBillable` deciding which side of every figure a row lands on. The included columns are
the only other columns the report reads from this table: `MatterId` reaches the client,
`DurationMinutes` feeds both hour totals, `HourlyRateSnapshot` feeds the amount. Together they
make the index covering.

`UserId` and `Narrative` are deliberately excluded. The report never reads them, and every
included column is paid for on every write.

The index ships in a migration, so a fresh clone has it. It arrived in feature 004 rather than
with the original schema on purpose: feature 003 shipped the rollup against the un-indexed table
so that the "before" measured here is the state the repository actually was in, not one
manufactured for the occasion.

---

## Results

Reproduced verbatim in `docs/performance/summary.txt`.

| Shape | Index | Logical reads | Elapsed median | Elapsed range | Rows |
| --- | --- | ---: | ---: | --- | ---: |
| Full range | without | **6,879** | 132 ms | 121–137 ms | 5,775 |
| Full range | with | **1,768** | 112 ms | 110–116 ms | 5,775 |
| Single client | without | **6,879** | 128 ms | 112–158 ms | 105 |
| Single client | with | **1,768** | 102 ms | 101–108 ms | 105 |

**Logical reads fall by 74%** — 6,879 to 1,768, a factor of 3.9. **Elapsed time falls by 15%.**

Those two numbers disagree, and the disagreement is the interesting part.

### Per table, full range

| Table | Without index | With index |
| --- | ---: | ---: |
| `TimeEntries` | 6,868 | 1,761 |
| `Matters` | 7 | 4 |
| `Clients` | 4 | 3 |
| `Worktable` | 0 | 0 |

Almost all of it is `TimeEntries`, which is what a covering index on that table should do. Its
scan count drops from **21 to 1** — the un-indexed plan was reading it across parallel threads.

---

## Plan shape

All four plans are committed as `.sqlplan` files and open in SSMS or Azure Data Studio. The two
shapes produced the same operator profile as each other in both states, so one table covers all
four.

| Operator | Without index | With index |
| --- | ---: | ---: |
| Clustered Index Scan | 2 | 1 |
| **Index Seek** | 0 | **1** |
| **Parallelism** | **1** | **0** |
| Sort | 4 | 3 |
| Hash Match | 4 | 4 |
| Window Aggregate | 3 | 3 |
| Compute Scalar | 5 | 5 |

Three changes, in order of how much they matter:

**The clustered index scan of `TimeEntries` becomes a seek on the covering index.** This is what
the index was added for. The un-indexed plan had to read the whole table — every column of every
one of 400,000 rows — to answer a question about four columns of a date range. The indexed plan
seeks the range in a structure holding only those four.

**The plan stops going parallel.** The un-indexed plan needed several threads to keep its wall
clock reasonable; the indexed one does the work serially and still finishes sooner. This is
where the 74%-versus-15% gap comes from, and it is the most important observation in this
document.

**One sort disappears.** Four become three. The index's key order supplies what one of them was
producing.

`Hash Match` and `Window Aggregate` are unchanged, as expected: the aggregation and the three
window functions are work no index removes. The index changes how rows are *reached*, not what
is *computed* from them.

---

## The number that matters most is CPU, not elapsed

From the committed raw captures, peak reported values within each:

| | Without index | With index |
| --- | ---: | ---: |
| **CPU time**, full range | **847 ms** | **105 ms** |
| Elapsed, full range | 137 ms | 116 ms |
| **CPU time**, single client | **811 ms** | **95 ms** |
| Elapsed, single client | 132 ms | 102 ms |

**CPU time falls by roughly a factor of eight. Wall-clock time falls by 15%.**

The un-indexed plan spent 847 ms of processor time to deliver 137 ms of wall clock — it was
parallelising its way around a full-table scan, burning six threads' worth of work to hide the
cost behind a shorter wait. The indexed plan does the same job with 105 ms of CPU on one thread.

On an idle laptop with cores to spare, that trade looks like a modest 15% improvement. On a
server running more than one query at a time, it is the difference between a report costing one
core-second and one costing eight. **The elapsed figure understates the change by roughly a
factor of five, and it is the figure most people would have quoted.**

### The single-client path reads exactly as much as the unfiltered one

6,879 without the index and 1,768 with it — identical to the digit, in both states.

Filtering to one client returns 105 rows instead of 5,775 and does not save a single page read.
The report ranks every client before narrowing to one, so the narrowing happens after all the
work is done. That is a deliberate design decision made in feature 003, so that a filtered
report still shows a client's true standing rather than "1 of 1", and this is what it costs.

It is also the reason this shape was measured separately. Assuming it behaved like the
unfiltered path would have produced a plausible and wrong sentence in this document.

---

## What these numbers do not say

- **One machine, one dataset size, one run of the protocol.** Elapsed times are not portable;
  read counts are.
- **No write-path measurement was taken.** The index is not free: every insert, and every update
  touching `WorkDate`, `IsBillable`, `MatterId`, `DurationMinutes` or `HourlyRateSnapshot`, now
  maintains a second structure. At this repository's write volume that cost is invisible, and it
  was not measured, so nothing is claimed about it.
- **The improvement is bounded by what an index can do.** Much of the remaining work is the
  aggregation, the three window functions and the sorts they need. No index removes those.
- **The parallelism finding depends on this machine's core count and cost threshold.** A server
  configured differently might not have gone parallel without the index, in which case the
  elapsed gap would look larger, not smaller.
- **The seed was not enlarged to improve the result.** `docs/prd.md` §8 offers that as a
  mitigation when the delta looks unimpressive. It was not taken: the dataset is committed, and
  feature 002's row-count criterion, verification bands and tests all assert its volumes. The
  measurement reports what is true of the repository as it ships.

---

## Reproducing this

From a cold clone, on a machine with Docker and the .NET 9 SDK:

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

```powershell
dotnet run --project src/LexTime.Api measure
```

That is all. No database tool, no benchmarking harness, no account. The verb drops the index,
measures, restores it, and leaves the database indexed however it ends — including if it fails
partway. `dotnet run --project src/LexTime.Api state` will confirm the index is back.

**What must match exactly**: the logical read counts, the row counts, and the result hashes.
They are properties of the plan and the data, and both are committed.

**What will legitimately differ**: every elapsed time, on every axis. Different processor,
different disk, different container memory, different neighbours.

Run it twice and compare: the read counts will be identical between your two runs and the
elapsed times will not. That contrast is the argument for how the two kinds of figure are
weighted here.

---

## Committed evidence

| File | What it is |
| --- | --- |
| `docs/performance/summary.txt` | The reduced figures — the results table above, as the run produced it |
| `docs/performance/statistics-{shape}-{state}.txt` | Verbatim `STATISTICS IO`/`TIME` output, four files |
| `docs/performance/plan-{shape}-{state}.sqlplan` | Actual execution plans with runtime counters, four files |

Every figure in every table above appears in one of these. The summary here is a transcription,
and a transcription is somewhere a number can quietly change — so the source sits next to it.

`summary.txt` also carries the result hashes, and they are worth a glance: the two index states
share a hash for each shape. That is the equivalence proof, over all 400,000 entries, which no
test in the suite can afford to load.
