---

description: "Task list for feature 003: Weekly Billable Rollup"
---

# Tasks: Weekly Billable Rollup

**Input**: Design documents from `/specs/003-weekly-billable-rollup/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts)

**Tests**: **Required, not optional.** Constitution P12 mandates a hand-computed fixture and
FR-021 to FR-024 spell out what it must contain. User Story 2 is entirely a testing story. The
tests in Phase 4 are the deliverable, not a check on it.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1, US2, US3 from [spec.md](./spec.md)
- Paths are repository-relative from `E:\LexTime`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Get the procedure file, the test fixture and the test helpers to the point where
everything else can be written against them.

- [X] T001 Create `db/programmability/usp_WeeklyBillableRollup.sql` as a `CREATE OR ALTER PROCEDURE` taking `@FromDate date`, `@ToDate date`, `@ClientId int = NULL` and returning the twelve columns of [contracts/usp-weekly-billable-rollup.md](./contracts/usp-weekly-billable-rollup.md) with correct types and **no aggregation logic** — a typed `SELECT ... WHERE 1 = 0`. This fixes the result-set contract first and makes every test in Phases 3–5 fail on wrong data rather than on a missing object. Per R3 the file holds exactly one statement: no `GO`, no `SET` preamble before the `CREATE OR ALTER`, `SET NOCOUNT ON` inside the body only
- [X] T002 Extend `tests/LexTime.IntegrationTests/SqlServerFixture.cs` to apply `db/programmability/*.sql` after `MigrateAsync()`, in both `InitializeAsync` and `CreateIsolatedDatabaseAsync`. Locate the repository root by walking up from `AppContext.BaseDirectory` for `LexTime.sln`, duplicating the six-line walk in `MaintenanceCommands.FindRepositoryRoot` — R8 records why duplication beats widening the API project's public surface for a third caller that does not exist yet
- [X] T003 [P] Widen `DirectSql.InsertTimeEntryAsync` in `tests/LexTime.IntegrationTests/DirectSql.cs` to take `isBillable` and `hourlyRate` as parameters, defaulting to the current hard-coded `true` and `350.00` so the existing constraint tests are untouched. FR-022 needs non-billable entries and two clients at different rates, and neither is expressible today (R9)

**Checkpoint**: the procedure exists and returns its contract shape; tests can call it.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The read path, wired end to end against the empty procedure from T001. This is the
layering constitution P4 and P5 require, and it blocks all three stories.

**⚠️ CRITICAL**: no user story work begins until this phase is complete.

- [X] T004 [P] Create `src/LexTime.Application/Reporting/WeeklyBillableRollupRow.cs` — an immutable record with the eleven fields in [data-model.md](./data-model.md). `HoursDeltaVsPriorWeek` is `decimal?` and its null is meaningful: absent means the prior week is outside the range, **not** zero. Document that on the property, because a consumer coalescing it to zero misreports every client's first week (P25)
- [X] T005 [P] Create `src/LexTime.Application/Reporting/WeeklyBillableRollupQuery.cs` — `From`, `To` as `DateOnly` and `ClientId` as `int?`. Both dates required; there is no open-ended report
- [X] T006 [P] Create `src/LexTime.Application/Reporting/WeeklyBillableRollupResponse.cs` — echoes `From` and `To` and carries `IReadOnlyList<WeeklyBillableRollupRow> Rows`, empty rather than null when nothing matched
- [X] T007 Create `src/LexTime.Application/Reporting/IWeeklyBillableRollupReader.cs` — one method taking a `WeeklyBillableRollupQuery` and a `CancellationToken`, returning the rows. Declared in `Application` and implemented in `Infrastructure`: this is the interface P5 requires and the first real content `LexTime.Application` has held
- [X] T008 Create `src/LexTime.Infrastructure/Reporting/SqlWeeklyBillableRollupReader.cs` implementing T007 with `SqlConnection` + `SqlCommand` + `SqlDataReader`, `CommandType.StoredProcedure`, and typed `SqlParameter`s — `@ClientId` passed as `DBNull.Value` when absent (R5). **No EF Core, no `FromSqlRaw`, no entity mapping**: this is the only place raw ADO.NET appears in the solution's read path. Read `HoursDeltaVsPriorWeek` through `IsDBNull` and preserve the null
- [X] T009 Create `src/LexTime.Application/Reporting/GetWeeklyBillableRollupHandler.cs` — takes the reader by constructor injection, invokes it, wraps the rows in the response envelope. One handler class for the one use case (P4)
- [X] T010 [P] Register `GetWeeklyBillableRollupHandler` in `src/LexTime.Application/DependencyInjection.cs`. Replace the comment saying the method registers nothing with what it now registers
- [X] T011 [P] Register `IWeeklyBillableRollupReader` → `SqlWeeklyBillableRollupReader` in `src/LexTime.Infrastructure/DependencyInjection.cs`, constructing it from the connection string that method has already resolved and validated (R11). Do not take the connection from `LexTimeDbContext` — routing the non-EF path through EF would defeat its purpose

**Checkpoint**: the read path resolves from DI and returns zero rows from a real procedure call.

---

## Phase 3: User Story 1 — A reviewer reads a firm's billing week by week (Priority: P1) 🎯 MVP

**Goal**: The report computes correctly and is reachable over HTTP. This is the artifact the
repository exists for (P10).

**Independent Test**: with a seeded database, request the report over the full seeded range and
inspect the rows — all twelve fields populated, chronological, busiest client first.

### Tests for User Story 1

> Write these first. Against T001's empty procedure they fail on zero rows, which is the
> correct starting state.

- [X] T012 [P] [US1] Create `tests/LexTime.IntegrationTests/RollupEndpointTests.cs` with a happy-path test: a valid token and a range covering seeded activity returns 200 and rows with every field populated, `HoursDeltaVsPriorWeek` null on the first reported week and a number afterwards
- [X] T013 [P] [US1] Add a test to `RollupEndpointTests.cs` asserting the single-client filter returns only that client's weeks **with `ClientRankInWeek` still reflecting its position among all clients** — a column of `1`s means ranking happened after filtering (FR-012)

### Implementation for User Story 1

- [X] T014 [US1] In `db/programmability/usp_WeeklyBillableRollup.sql`, add the `WeeklyTotals` CTE: join `TimeEntries` → `Matters`, filter on the inclusive date range, group by `ClientId` and `WeekIndex = DATEDIFF(day, '19000101', WorkDate) / 7`, and split billable from non-billable minutes. Sum minutes as integers and convert once (R10). Comment the anchor date: `DATEPART(weekday, …)` is deliberately avoided because it moves with `SET DATEFIRST` (R1)
- [X] T015 [US1] Add the windowing CTE: `SUM() OVER (PARTITION BY ClientId ORDER BY WeekIndex ROWS UNBOUNDED PRECEDING)` for the running total, `LAG()` twice for the previous week index and hours, and `DENSE_RANK() OVER (PARTITION BY WeekIndex ORDER BY BillableHours DESC)` computed **before** any client filter. P10 requires a comment where the frame is non-obvious: state why `ROWS` rather than the default `RANGE`, and why the rank is computed here rather than after filtering
- [X] T016 [US1] Add the final select: derive `IsoYear` from the week's Thursday and `IsoWeek` from `DATEPART(ISO_WEEK, …)`, join `Clients` for code and name, apply `HoursDeltaVsPriorWeek`'s three-branch `CASE` in the order R2 specifies, apply `(@ClientId IS NULL OR ClientId = @ClientId)`, and order by `WeekIndex, ClientRankInWeek, ClientCode`. Comment the catch-all predicate's plan-reuse trade-off and why `OPTION (RECOMPILE)` is deliberately absent (R6)
- [X] T017 [US1] Create `src/LexTime.Api/Endpoints/ReportEndpoints.cs` exposing `MapReportEndpoints()` as an extension method (P21), registering `GET /api/v1/reports/weekly-billable-rollup` bound to `from`, `to` and optional `clientId`, invoking the handler and returning the envelope
- [X] T018 [US1] In `src/LexTime.Api/Program.cs`, call `app.MapReportEndpoints()` and **delete the `/api/v1/ping` placeholder and its comment** — that comment says it goes when the first real endpoint lands, and this is it (R9)
- [X] T019 [US1] Retarget `tests/LexTime.IntegrationTests/AuthBoundaryTests.cs` from `/api/v1/ping` to the rollup route, supplying `from` and `to` on the accepted-token test. All four assertions keep their meaning and get stronger: the boundary is now proven on a route that returns real data. Forced by T018 — leaving it would break the build

**Checkpoint**: the report is correct enough to demonstrate and reachable over HTTP. MVP.

---

## Phase 4: User Story 2 — The numbers can be trusted without trusting the report (Priority: P2)

**Goal**: Every derived figure verified against expectations a human computed. This story is
why constitution P12 exists and is **not cuttable** (see plan.md's cut order).

**Independent Test**: load a small fixture with known content, call the procedure directly, and
compare every field against literals written into the test before the procedure was run.

### Tests for User Story 2

> These call `dbo.usp_WeeklyBillableRollup` **directly**, not the endpoint (SC-009). A test
> going through HTTP asserts six components at once and names none of them when it fails.
>
> The expectations are computed on paper from the fixture's inputs and written down **before**
> the procedure is run against it. Where the two disagree, assume the procedure is wrong (R7).

- [X] T020 [US2] Create `tests/LexTime.IntegrationTests/RollupFixtureBuilder.cs` building the hand-computed dataset on an isolated database: a handful of clients over a known span of weeks, covering every case FR-022 and FR-023 require. **The gap client's week before the gap must have a different, non-zero billable total from its returning week** — if the two coincide, the gap test passes under both the correct implementation and the wrong one and proves nothing (R7). Document each client's role in the fixture's XML comments, since the expectations elsewhere are unreadable without it
- [X] T021 [US2] Create `tests/LexTime.IntegrationTests/WeeklyBillableRollupTests.cs` asserting the full result set for the fixture field by field against hand-computed literals — billable and non-billable hours, amount, running total, delta and rank on every row (FR-021, SC-002)
- [X] T022 [P] [US2] Add a test asserting a week in which a client logged only non-billable time still appears, with zero billable hours, zero amount, and its non-billable hours intact
- [X] T023 [P] [US2] Add a test asserting a range containing no entries returns zero rows and does not error (FR-024). Accumulating calculations commonly fail on the empty case in a way no populated test detects
- [X] T024 [US2] Add the gap test: a client that bills, goes silent for several weeks, then bills again. Assert the returning week's delta equals **its own billable hours**, and assert separately that it does **not** equal the difference against the week the client last billed in. Both assertions are required — the first alone passes under either implementation when the fixture is careless (FR-022)
- [X] T025 [US2] Add the year-boundary gap test: the same shape spanning New Year (FR-023). ISO week 1 follows week 52 or 53 depending on the year, so any "preceding week" derived from the week number is wrong every January and right the rest of the time. Assert the week beginning Monday 2025-12-29 is reported as ISO year **2026** week **1**, and that its delta is measured against the week beginning 2025-12-22
- [X] T026 [P] [US2] Add a test asserting two clients tied on billable hours in a week share a rank and the next client takes the following position, not the one after it (FR-009, dense not sparse)
- [X] T027 [P] [US2] Add a test asserting a range boundary falling mid-week reports that week with only its in-range days, and that the first reported week's delta is null because the preceding week is outside the range
- [X] T028 [US2] Add a test against the **seeded** database asserting at least one client whose current status is inactive appears in the report for a period during which it billed (FR-010, SC-007). Feature 002 guarantees such clients exist; this is the test that stops a later "filter out inactive clients" change from passing silently

**Checkpoint**: the report's arithmetic is independently verified, including the two boundary
interactions that only fail together.

---

## Phase 5: User Story 3 — A malformed or unauthorised request fails clearly (Priority: P3)

**Goal**: A bad request gets a specific refusal rather than an empty result or a stack trace.

**Independent Test**: issue requests with a missing date, an inverted range, an unknown client
and no credentials, and inspect each response.

- [X] T029 [US3] Add validation to `src/LexTime.Api/Endpoints/ReportEndpoints.cs`: both dates required with no default range assumed, and `from` later than `to` refused with `application/problem+json` naming both values (FR-018, FR-019). The procedure deliberately does not validate this — an inverted range there simply matches nothing; the actionable message belongs at the boundary
- [X] T030 [P] [US3] Add tests to `RollupEndpointTests.cs` for `from` later than `to`, `from` omitted and `to` omitted — each 400 with a problem body naming what was wrong (SC-008)
- [X] T031 [P] [US3] Add a test asserting `clientId` matching no client returns 200 with an empty row set, **not** 404 (FR-020). The report is over a period, and a client with nothing in that period legitimately produces nothing

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T032 Read `db/programmability/usp_WeeklyBillableRollup.sql` line by line against [contracts/usp-weekly-billable-rollup.md](./contracts/usp-weekly-billable-rollup.md) and R1–R2 (P15). This is the exact category P15 names — SQL involving window functions and joins, whose expected values the agent derived itself — and a green test run does not discharge it
- [ ] T033 Security review of everything this feature touches, recorded in `docs/agent-log.md` (P24): confirm every parameter crosses as a typed `SqlParameter` with nothing concatenated, that `CommandType` is `StoredProcedure`, that the endpoint carries no `AllowAnonymous`, and that removing `/api/v1/ping` left no route outside the fallback-closed policy. **Per R5 a new CA2100 suppression is a design error here, not something to justify** — the procedure takes three scalar parameters and its command text does not vary
- [ ] T034 Delete `db/programmability/.gitkeep`, whose comment reads "Empty until feature 003". The directory now has content and the file's reason for existing is gone
- [ ] T035 [P] Add feature 003 entries to `docs/agent-log.md` for whatever went wrong during implementation, with symptom and how it was caught (P16). If nothing did, say so rather than inventing friction
- [ ] T036 [P] Add a rollup section to `README.md`: what the endpoint returns, the three window functions and what each is for, and the two positions this feature took — inactive clients included, and the delta measured against the calendar week. **No performance numbers, no plan shapes, no "fast" (P8)** — the measurement is the next feature and this one ships un-indexed on purpose
- [ ] T037 Run `dotnet build --warnaserror --no-incremental` and confirm `0 Warning(s), 0 Error(s)`. Use `--no-incremental`: an incremental build has previously reported a clean gate that a full build did not
- [ ] T038 Run `dotnet test` and confirm green, with the 40 tests from features 001 and 002 still passing alongside the new ones
- [ ] T039 Walk `quickstart.md` end to end on a reset environment (P18): confirm `apply-procedures` now reports `1 applied` where it previously reported `no procedures to apply`, that the quickstart is still exactly two commands, and that both direct-procedure calls and both endpoint calls behave as documented

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies
- **Phase 2 (Foundational)** — needs T001 for the procedure to call; **blocks all stories**
- **Phase 3 (US1)** — needs Phase 2
- **Phase 4 (US2)** — needs Phase 2 for the reader, and T014–T016 for anything to verify
- **Phase 5 (US3)** — needs T017
- **Phase 6 (Polish)** — needs the stories that are being shipped

### Story dependencies

- **US1 (P1)** — independent once Phase 2 is done. Delivers the MVP alone
- **US2 (P2)** — verifies US1's procedure, so it follows it in practice. Its tests call the
  procedure directly and need neither the endpoint nor US3
- **US3 (P3)** — needs only the endpoint from T017, not the report's correctness. Genuinely
  independent of US2

### Within each story

- Tests before implementation. Against T001's empty procedure they fail on data, not on a
  missing object — which is the useful kind of red
- Procedure before reader before handler before endpoint
- T019 is not optional and not deferrable: T018 deletes the route those tests point at

### Parallel opportunities

- **T004, T005, T006** — three records, three files, no dependencies
- **T010, T011** — two registration files in two projects
- **T012, T013** — both add to the same new file; parallel only if written as one task
- **T022, T023, T026, T027** — four independent test cases once T020 and T021 exist
- **T030, T031** — independent validation cases
- **T035, T036** — two documentation files

Phase 1's T002 and T003 both touch the test project but not the same file, so T003 is marked
`[P]`. T001 blocks T002 in practice: applying an empty procedure directory would not exercise
the new code path.

---

## Implementation Strategy

### MVP first

1. Phase 1 → Phase 2 → Phase 3
2. **Stop and validate**: request the report over the seeded range and read the rows
3. At this point the feature demonstrates, but is not yet trustworthy — US2 is what makes the
   numbers evidence rather than output

### If the evening runs out

plan.md fixes the cut order so it is not improvised here:

1. **Cut first**: T013, T027, T030, T031 — endpoint-level coverage thins to one happy path and
   the 401s that T019 already provides
2. **Cut second**: the `clientId` endpoint parameter (T013's subject), with the procedure
   keeping `@ClientId` and the endpoint exposing it next feature
3. **Never cut**: T014–T016, T020–T021, T024–T025, T032. The procedure, the hand-computed
   fixture, the two gap cases and the line-by-line review *are* the feature. P10 and P12 say so,
   and P3's own text cuts the spec before it cuts the rule

### Commit shape (P17)

Plan and tasks land separately from implementation. Within implementation, commit per phase
rather than per task, so the log reads as the sequence above rather than as forty entries.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- Every new type, method, parameter and test method carries an XML doc comment (P25). A comment
  restating its signature is a defect, not compliance
- Verify tests fail before implementing — and check *why* they fail. A test failing because the
  procedure does not exist is not the same as one failing on a wrong number
- The seeded database is the fixture for T028 only. Everything else in Phase 4 builds its own
  isolated database, so a reseed cannot silently change an expectation
