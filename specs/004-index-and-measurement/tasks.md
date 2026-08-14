---

description: "Task list for feature 004: Index and Measured Performance"
---

# Tasks: Index and Measured Performance

**Input**: Design documents from `/specs/004-index-and-measurement/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts)

**Tests**: **Required.** FR-003 makes result-set equivalence the thing this feature must not get
wrong, and user story 1 is entirely about proving it. The test in Phase 3 is a deliverable, not
a check on one.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1, US2, US3 from [spec.md](./spec.md)
- Paths are repository-relative from `E:\LexTime`

**One rule that governs the whole list**: no task may write a performance figure into any
document until the run that produced it exists. Constitution P8 is not satisfied by a number
that is later made true.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Get the index into the schema so everything downstream has something to toggle.

- [X] T001 Declare the covering index in `src/LexTime.Infrastructure/Persistence/Configurations/TimeEntryConfiguration.cs` — keyed on `WorkDate` and `IsBillable`, including `MatterId`, `DurationMinutes` and `HourlyRateSnapshot`, named `IX_TimeEntries_WorkDate_Billable`. **Replace, do not delete, the comment currently explaining that the index is deliberately absent**: it was true when written and is now false, and the reason the index arrived in its own feature is worth keeping (R1)
- [X] T002 Generate the EF migration for T001 into `src/LexTime.Infrastructure/Persistence/Migrations/` and read the generated file before accepting it — confirm it creates exactly the one index and touches nothing else. `dotnet-ef` is an authoring tool here; it stays out of the quickstart, which applies migrations in-process (feature 002 R0)
- [X] T003 [P] Correct the stale forward reference in `src/LexTime.Infrastructure/Seeding/SeedOptions.cs`, whose remarks credit "feature 003's index measurement" — that measurement is this feature (R10)

**Checkpoint**: a fresh clone's schema carries the index; `dotnet test` still green.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The measurement machinery. Blocks all three stories.

**⚠️ CRITICAL**: no user story work begins until this phase is complete.

- [X] T004 Create `src/LexTime.Infrastructure/Measurement/CoveringIndex.cs` holding the index name, its `CREATE` and `DROP` statements as constants, a presence check against `sys.indexes`, and idempotent create/drop helpers. Constants, not composed strings — R5 of feature 003 applies unchanged and a `CA2100` suppression here would be a design error
- [X] T005 [P] Create `src/LexTime.Infrastructure/Measurement/MeasurementReading.cs` with the `IndexState` and `RequestShape` enumerations and the reading and combination records from [data-model.md](./data-model.md). Document on `LogicalReads` that it is deterministic and on `ElapsedMilliseconds` that it is not — that asymmetry is the whole reporting convention
- [X] T006 Create `src/LexTime.Infrastructure/Measurement/RollupMeasurer.cs` able to run one reading: clear the buffer pool, execute the procedure with `SET STATISTICS IO`/`TIME` on, collect the messages through `SqlConnection.InfoMessage`, and parse the logical reads and elapsed time out of them. Keep the raw message text — it is committed verbatim later and is the reason the summary table can be audited (R3)
- [X] T007 Add plan capture to `RollupMeasurer`: a separate execution with `SET STATISTICS XML ON`, reading the plan from the additional result set. It is the **actual** plan with runtime counters, verified in R4. A second execution rather than one combined pass, because interleaving plan and data result sets buys nothing here and costs clarity
- [X] T008 [P] Register the measurer in `src/LexTime.Infrastructure/DependencyInjection.cs`, taking the connection string the method has already resolved — the same shape as the rollup reader
- [X] T009 Add the `measure` verb to `src/LexTime.Api/Maintenance/MaintenanceCommands.cs`: add it to `KnownVerbs`, parse `--readings`, `--output` and `--skip-single-client`, **ensure the index exists before doing anything else**, and restore it in a `finally`. Both defences are required and neither replaces the other — see R7 and the contract at [contracts/measure-verb.md](./contracts/measure-verb.md)
- [X] T010 Extend `src/LexTime.Infrastructure/Maintenance/DatabaseStateInspector.cs` to report whether the covering index is present, and surface it in the `state` verb's output. The third R7 defence: `state` is where a developer asks what condition the database is in, so it is where "the migration is applied but the index is gone" has to be answerable

**Checkpoint**: `measure` runs, produces readings for one combination, and leaves the index in place.

---

## Phase 3: User Story 1 — The report is unchanged by the index (Priority: P1) 🎯 MVP

**Goal**: Prove the index changed nothing about what the report returns, at both scales.

**Independent Test**: run the rollup, drop the index, run it again, compare. Delivers value
alone — a faster report whose correctness is demonstrably untouched.

### Tests for User Story 1

> Write these first. Against the schema from Phase 1 the presence test passes immediately; the
> equivalence test is the one that must be seen to exercise both states.

- [X] T011 [P] [US1] Create `tests/LexTime.IntegrationTests/CoveringIndexTests.cs` with a test asserting the index exists on a freshly migrated database, by name and with the expected key and included columns — not merely that an index of that name exists, or a renamed stub would pass
- [X] T012 [US1] Add the equivalence test to `CoveringIndexTests.cs`: seed a small fixture, run the rollup, drop the index, run again, compare every field of every row **including order**, then restore the index. Assert the restore in the test itself, so a failure cannot leave the shared container's database degraded for the tests that follow

### Implementation for User Story 1

- [X] T013 [US1] Add result hashing to `RollupMeasurer` — a stable hash over the ordered result set — and compare the two index states' hashes in the `measure` verb. A mismatch exits `VerificationFailed` and says which shape disagreed. This is SC-001's full-scale claim: no test can load 400,000 entries, and the measurement reads both result sets anyway (R8)
- [X] T014 [US1] Run the full existing suite and confirm every feature-003 test passes **with no expected value edited**. Under FR-004, an edit to any expected figure in `WeeklyBillableRollupTests` is a defect in this feature, not an update — a test adjusted to agree with the index has been made useless

**Checkpoint**: the index ships and is proved harmless. MVP.

---

## Phase 4: User Story 2 — A reviewer regenerates the numbers (Priority: P2)

**Goal**: The measurement runs end to end, produces committed evidence, and the account is
published.

**Independent Test**: follow `quickstart.md` on a seeded environment and compare the read counts
obtained against the published ones.

- [X] T015 [US2] Add cache control to each reading in `RollupMeasurer`: `CHECKPOINT` then `DBCC DROPCLEANBUFFERS`, applied identically to both index states. Print the instance-wide warning from [contracts/measure-verb.md](./contracts/measure-verb.md) before the first reading — it clears the buffer pool for the whole server, which is fine on the quickstart's container and unwelcome anywhere else (R5)
- [X] T016 [US2] Take five readings per combination in the `measure` verb and reduce them: logical reads as a single figure because every reading agrees, elapsed time as a median with its minimum and maximum. If the read counts ever disagree between readings, fail rather than average — that would mean something is varying that should not (R6)
- [X] T017 [P] [US2] Write the verbatim statistics capture to `docs/performance/statistics-{shape}-{state}.txt` — exactly as the server sent it, unreformatted, unrounded, untrimmed. This file is what makes the published table auditable rather than merely believable (R3)
- [X] T018 [P] [US2] Write the captured plans to `docs/performance/plan-{shape}-{state}.sqlplan`, openable in SSMS or Azure Data Studio (FR-008)
- [X] T019 [US2] Print the summary table to stdout: combination, logical reads, elapsed median and range, row count, equivalence verdict, then the paths written
- [X] T020 [US2] **Run the measurement** against the fully seeded local database and commit the artefacts it produced. This is the first task in the feature permitted to produce a number
- [X] T021 [US2] Write `docs/performance.md` from the captured files, following [contracts/performance-document.md](./contracts/performance-document.md): method before results, the index and what each column is for, the results table, the plan-shape paragraph, the honest limits, and the reproduction steps. **Every figure traced to a committed raw file.** If the improvement is modest, say so and say why — FR-018 forbids reaching for a bigger dataset instead
- [X] T022 [US2] Replace the four `TODO(measure)` placeholders in `README.md` with captured figures, and update the status banner. No placeholder may remain anywhere in the repository (FR-019)
- [X] T023 [US2] Run `measure` a second time and confirm the logical read counts and result hashes are identical to the first run while the elapsed times differ. That contrast is the evidence for how the two kinds of figure are weighted, and SC-004 is the assertion (FR-016)

**Checkpoint**: the numbers exist, are published, and reproduce.

---

## Phase 5: User Story 3 — The single-client path is measured on its own (Priority: P3)

**Goal**: Measure the path that ranks every client before narrowing to one.

**Independent Test**: measure the single-client request in both index states and compare its
change against the full-range request's.

- [X] T024 [US3] Add the single-client request shape to the `measure` verb — the same full range filtered to the busiest client — and implement `--skip-single-client` as the escape hatch the plan's cut order names. The busiest client rather than an arbitrary one, so the shape is reproducible and its row count is not near zero (R9)
- [X] T025 [US3] Re-run the measurement so all four combinations are captured, and commit the two additional plans and statistics files
- [X] T026 [US3] Extend `docs/performance.md` to four rows and add the comparison: does the index help this path more, less, or the same as the unfiltered one, and what in the plans accounts for the difference. This is the question feature 003 flagged when it put ranking before filtering

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T027 Read the index DDL and every statement in `Measurement/` line by line (P15). The DDL, the `sys.indexes` probe, the `DBCC` and the procedure invocation are all SQL this feature generated, and a green measurement run does not discharge the review
- [X] T028 Security review recorded in `docs/agent-log.md` (P24): confirm the index statements and the presence check are constants with nothing concatenated, that the procedure is still invoked with typed parameters, and that the `measure` verb takes no input that reaches SQL. **A new `CA2100` suppression is a design error here, not a finding to justify**
- [X] T029 [P] Add feature 004 entries to `docs/agent-log.md` for whatever went wrong, with symptom and how it was caught (P16). If nothing did, say so rather than inventing friction
- [X] T030 Walk `quickstart.md` end to end: `state` reports the index, the equivalence test passes, `measure` reproduces, and every figure in the summary table is findable in the raw statistics files. Validation 4 is the one that matters — it is the check a reviewer will actually perform
- [X] T031 Run `dotnet build --warnaserror --no-incremental` and confirm `0 Warning(s), 0 Error(s)`. `--no-incremental` because an incremental build has previously reported a clean gate that a full build did not
- [X] T032 Run `dotnet test` and confirm green, with feature 003's tests passing unchanged
- [X] T033 [P] Add a closing hint for `measure` to `scripts/Initialize-LocalDb.ps1`, so a developer who has just seeded the database learns the verb exists without reading the specs

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies
- **Phase 2 (Foundational)** — needs T001–T002 for an index to toggle; **blocks all stories**
- **Phase 3 (US1)** — needs Phase 2
- **Phase 4 (US2)** — needs Phase 2, and T013 for the equivalence verdict the run reports
- **Phase 5 (US3)** — needs Phase 4's capture and publication to extend
- **Phase 6 (Polish)** — needs the stories being shipped

### Story dependencies

- **US1 (P1)** — independent once Phase 2 is done. Delivers the MVP alone: the index ships,
  proved harmless
- **US2 (P2)** — needs US1's hashing for its equivalence verdict, and is otherwise independent
- **US3 (P3)** — extends US2's run and document rather than standing beside them. This is
  deliberate: it is the first thing the plan's cut order drops, and structuring it as an
  extension means dropping it leaves a coherent two-row document rather than a hole

### Within each story

- Tests before implementation
- The index before anything that toggles it
- **Nothing writes a figure before T020.** T021 and T022 are the only tasks that publish numbers
  and both come after the run that produces them

### Parallel opportunities

- **T003** — a comment fix in a file nothing else in Phase 1 touches
- **T005, T008** — a new records file and a registration line, independent of each other
- **T011** — the presence test, independent of the equivalence test in the same file only if
  written as one task; marked `[P]` because it can be written before T012
- **T017, T018** — two writers, two file types, no shared state
- **T029, T033** — a documentation file and a script

Most of this feature is sequential. The measurement is one run against one database and the
document is written from its output, so the parallelism is genuinely limited rather than
under-identified.

---

## Implementation Strategy

### MVP first

1. Phase 1 → Phase 2 → Phase 3
2. **Stop and validate**: the index is in the schema and the report returns exactly what it did
   before, at both scales
3. At this point the repository is faster and no claim has been made about it — which is the
   correct state to be in before any number exists

### If the evening runs out

The plan fixes the cut order so it is not improvised here:

1. **Cut first**: Phase 5 entirely. Two combinations instead of four; the document loses a
   comparison and keeps its structure
2. **Cut second**: T016's five readings drop to three. The elapsed range widens; the read counts,
   being deterministic, are untouched
3. **Never cut**: T001–T002, T012–T013, T017–T018, T021, T027. The index, the equivalence proof
   at both scales, the committed evidence, the honest account, and the line-by-line review

### Commit shape (P17)

Plan and tasks land separately from implementation. Within implementation, commit per phase.
**The captured artefacts and the document that cites them belong in the same commit** — a commit
containing figures whose evidence arrives later is exactly the gap P8 exists to close.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- Every new type, method, parameter and test method carries an XML doc comment (P25)
- The measurement clears the SQL Server buffer pool for the whole instance. Fine on the
  quickstart's container; do not point it at anything shared
- If the index turns out to help less than hoped, **that is the result**. T021 publishes it with
  an explanation. Enlarging the seed to improve it is forbidden by FR-018 and would invalidate
  feature 002's committed dataset and every test asserting its volumes
