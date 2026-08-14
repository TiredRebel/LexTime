# Research — Index and Measured Performance (004)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected.

**A note this feature needs more than the others.** Constitution P8 governs what may be
published as a performance figure, and it applies to this document as strictly as to
`docs/performance.md`. **Nothing here quotes a read count, an elapsed time or a ratio.** The
probes below verified that the *mechanisms* work; they were run without cache control, once
each, outside the protocol in R5. Any number they produced would be a number taken outside the
method and is therefore not a measurement. The measurement happens during implementation, under
the protocol, and its output goes to `docs/performance.md`.

---

## R1. The index ships in a migration

**Decision.** Declare the index in `TimeEntryConfiguration` and generate an EF migration for it.
A fresh clone applying the schema receives the index without any extra step.

**Rationale.** It is a table structure, and constitution P7 keeps only *procedures* out of
migrations — precisely because they are not schema. The index is.

The alternative — ship the schema un-indexed and apply the index by a separate script so the
"before" state is the default — was considered and rejected. It would leave every clone slower
than the README says the repository is, permanently, in order to preserve a state that only
matters for a measurement taken once. The un-indexed state is something the measurement creates
deliberately and briefly (R4), not something the repository lives in.

**Consequence for `TimeEntryConfiguration`.** It currently carries a comment explaining that
the index is *deliberately absent* and that adding it would destroy the before/after
comparison. That comment was correct when written and is now false. It must be replaced, not
deleted — the reason the index arrived in its own feature is worth keeping.

---

## R2. The measurement runs in-process as a host CLI verb

**Decision.** A new `measure` verb on `MaintenanceCommands`, joining the six from feature 002.
It uses the connection string the quickstart already configures.

**Rationale.** FR-015 says the measurement must need no tool the quickstart does not already
need. That rules out `sqlcmd` on the host, SSMS, Azure Data Studio, and any benchmarking
utility. It leaves two options: a PowerShell script driving `docker exec`, or code in the
application.

The verb wins on three counts. It works against any reachable SQL Server rather than only a
container the script can name. It reuses the configuration validation, exit codes and error
reporting that features 001 and 002 already built. And it is the pattern this repository
already chose for exactly this problem — the argument that kept `dotnet-ef` out of the
quickstart applies unchanged.

**P4 note.** `MaintenanceCommands` lives in `LexTime.Api` and will name `Infrastructure` types.
That is permitted: the amended P4 (v2.0.1) enumerates this file as one of three that may, and
the rule it states — no *endpoint* may — is untouched here.

---

## R3. Statistics are captured from the connection's info messages

**Decision.** `SET STATISTICS IO ON` and `SET STATISTICS TIME ON`, with the output collected
through `SqlConnection.InfoMessage`. The raw message text is committed verbatim alongside the
summary table.

**Verified**, in-process against the running container, not inferred: the handler receives the
statistics output, including lines containing `logical reads` and lines containing
`elapsed time`. The message shapes are

```
Table '<name>'. Scan count <n>, logical reads <n>, physical reads <n>, read-ahead reads <n>, ...
   CPU time = <n> ms,  elapsed time = <n> ms.
```

**Rationale.** `docs/prd.md` §6.6 asks by name for "actual `SET STATISTICS IO` logical reads"
and "actual `SET STATISTICS TIME` elapsed ms". This is that output, unmediated.

**Committing the raw text verbatim is the point, not a convenience.** A summary table is a
transcription, and a transcription is a place a number can quietly change. With the raw output
beside it, every figure in the table can be checked against its source by a reader who does not
trust the table — which is the entire posture P8 asks this repository to take.

**Alternatives rejected.**

- *`sys.dm_exec_query_stats`.* Gives totals per cached plan and would be easier to parse, but it
  is not what the done criterion names, and it aggregates across executions in ways that would
  need explaining away.
- *Parsing only, discarding the raw text.* Saves a file and removes the reader's ability to
  audit the table.

---

## R4. Plans are captured as actual plans, in-process, and committed as files

**Decision.** `SET STATISTICS XML ON`, reading the plan from the additional result set the
procedure's execution produces, written to `docs/performance/*.sqlplan`.

**Verified** in-process: the execution returns two result sets, the plan is retrievable from
them, and it contains runtime counters — so it is the **actual** plan with real row counts, not
an estimate. A `.sqlplan` file opens directly in SSMS or Azure Data Studio.

**Rationale.** FR-008 requires plans a reviewer can open and inspect rather than prose or an
image. An estimated plan would be cheaper to obtain and worth less: the interesting part of this
comparison is what actually happened, including whether a sort spilled.

**One implementation note that cost a probe to establish.** `sqlcmd` does not display the plan
result set in its default output, which makes it look as though `SET STATISTICS XML ON` is not
producing one. It is; the client is not showing it. Verifying through the client the
implementation will actually use, rather than through the one that was convenient, is what
separated "this does not work" from "I checked it wrong".

**Alternatives rejected.**

- *`SET SHOWPLAN_XML ON`.* Returns the estimated plan without executing. Simpler — no
  interleaved result sets — and answers a weaker question.
- *Plan cache via `sys.dm_exec_query_plan`.* Also estimated, and dependent on what happens to be
  cached at the moment it is queried.

---

## R5. Both states are measured from a cold buffer pool, identically

**Decision.** Before every measured run: `CHECKPOINT`, then `DBCC DROPCLEANBUFFERS`. Applied to
both index states without exception.

**Verified**: the container's `sa` login can execute it.

**Rationale.** The comparison is only meaningful if the two states start from the same place.
Measuring one warm and one cold would compare the cache, and whichever ran second would win.
Cold-for-both is the convention that needs the least explaining.

Logical reads are unaffected by this choice — they count buffer accesses whether or not the page
had to be fetched — so the primary evidence would be identical under any convention. It is
elapsed time that needs the control, which is also the figure that deserves the least weight
(R6). The convention is stated in `docs/performance.md` rather than left implicit.

**Stated as a caution, not buried.** `DBCC DROPCLEANBUFFERS` clears the buffer pool for the
whole instance, not one database. That is harmless on the local single-purpose container the
quickstart brings up and is unwelcome anywhere else. The measurement document and the verb's own
output both say so.

---

## R6. Five readings; read counts lead, elapsed time follows

**Decision.** Each combination is run five times. Read counts are reported as a single figure —
they are deterministic and every reading agrees. Elapsed times are reported as a median with the
full range beside it.

**Rationale.** FR-011 forbids publishing one elapsed time that happened to be observed once, and
FR-012 requires the two kinds of figure to be weighted differently and the reason stated.

Logical reads are a property of the plan: identical on every machine, every run, forever.
Elapsed time is a property of the hardware, the other processes on it, and what the container
had cached. Presenting them side by side with equal authority invites a reviewer to check the
one that does not reproduce, find it does not, and discount both. The document says which is
which, in those terms.

Five rather than three: enough for a median to mean something, cheap enough that four
combinations remain a short run. If the evening is tight, the plan's cut order drops this to
three, which widens the range without touching the read counts.

---

## R7. A dropped index is not restored by re-running migrations

**Decision.** The `measure` verb ensures the index exists when it starts, restores it in a
`finally`, and the `state` verb reports its presence.

**Rationale — this is the trap, and it is not obvious.** Once the index is created by a
migration, EF records that migration as applied. Drop the index by hand and re-run `migrate` and
**nothing happens**: EF compares migration history, not schema. The database then looks fully
migrated, reports itself as such, and is missing an index — and the only symptom is that the
report is slower than the repository claims, which nobody notices.

An interrupted measurement is exactly how that state gets created. So three defences, none of
them expensive:

1. **`finally`** — the ordinary path, covering exceptions and cancellation.
2. **Ensure-on-entry** — the verb creates the index if it is missing before doing anything else,
   so a database left broken by a previous crash heals on the next run rather than silently
   producing a "with index" measurement that was taken without one.
3. **`state` reports it** — the one place a developer looks to ask what condition the database
   is in should be able to answer this question.

Defence 2 is the one that matters most and is the least obvious: without it, a crashed run
followed by a clean run produces four measurements of which two are quietly mislabelled.

This is the same class of problem as feature 002's partial seed — a database that looks complete
and is not — and it is worth noting that the earlier feature's answer was a state inspector,
which is why extending it here costs three lines.

---

## R8. Equivalence is proved at both scales, for different reasons

**Decision.**

- **A row-by-row test** at 1/100 scale in `CoveringIndexTests`: run the rollup, drop the index,
  run again, compare every field of every row, restore.
- **A full-scale checksum** inside the `measure` verb: the result set of each state is hashed
  and the two hashes compared, over all 400,000 entries and all 24 months.

**Rationale.** SC-001 claims equivalence "across all 24 months", and a test that loaded 400,000
rows would take minutes and would not be run often enough to catch a regression. A test that ran
only at small scale would leave the actual claim unverified. Doing both costs little and each
covers the other's gap: the small test says *what* differs when something does, the full-scale
hash says *whether* anything differs at the scale the claim is about.

The measurement has to run the query in both states anyway. Hashing the rows it is already
reading is close to free.

**Why this matters more than it sounds.** An index that changes results is the failure that
hides best — every figure stays plausible and self-consistent, exactly as with the
window-function errors P12 was written for. FR-003 exists for that reason and it is why the
equivalence story is US1 rather than an afterthought in the polish phase.

---

## R9. Four combinations, and the exact range

**Decision.** Two index states × two request shapes:

| Shape | Parameters |
| --- | --- |
| Full range, all clients | `2024-08-13` to `2026-08-13`, no client filter |
| Full range, one client | the same dates, filtered to the busiest client |

The dates are the seed's full window: `SeedOptions.DefaultReferenceDate` is 2026-08-13 and
`MonthsOfHistory` is 24, so `EarliestDate` is 2024-08-13. Both are committed constants, which is
what makes the run repeatable.

**Rationale for measuring the single-client shape separately.** The report ranks every client
before narrowing to one (feature 003 FR-012), so a single-client request does the
full-population aggregation and discards most of the result. Whether the index helps that path
as much, more, or less than the unfiltered one is not predictable from the unfiltered
measurement — and a performance section that measured only the obvious query would be reporting
the easy half. Feature 003's research flagged this path; this is where it gets checked.

The busiest client is chosen rather than a random one so the shape is reproducible and its row
count is not near zero.

---

## R10. Two stale forward references to correct

Both were written before feature 003 was split from its performance half and now point at the
wrong feature. Neither is load-bearing; both are the sort of small contradiction a reviewer can
find, in a repository whose whole argument is that its claims are checkable.

| File | Says | Should say |
| --- | --- | --- |
| `SeedOptions.cs` remarks | "makes feature 003's index measurement comparable" | this feature's |
| `TimeEntryConfiguration.cs` comment | "the baseline feature 003 measures the rollup against" | replaced wholesale by R1 — the index is no longer absent |

---

## Open questions

None. The spec's single `NEEDS CLARIFICATION` was closed in its clarification session before
this plan began, and no new one arose during design.
