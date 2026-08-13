---

description: "Task list for feature 002: Bootstrap and Seed"
---

# Tasks: Bootstrap and Seed

**Input**: Design documents from `/specs/002-bootstrap-and-seed/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/host-cli.md](./contracts/host-cli.md),
[contracts/bootstrap-cli.md](./contracts/bootstrap-cli.md)

**Tests**: Included. FR-023 requires verification, and the plan's Constitution Check makes
the generator testable at 1/100 scale by parameterising volume — determinism is the
property most worth a test and the least visible by eye.

**Organization**: Grouped by user story so each is independently implementable and
testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)

## Path Conventions

Feature 001's four projects plus tests already exist. This feature adds no project and no
package.

> **Applies to every C# task**: constitution **P25** requires an XML documentation comment
> on every type, method, property, parameter, return value and documented exception —
> private members and test methods included. `GenerateDocumentationFile` is already on, so
> an undocumented member fails the build. The PowerShell script's equivalent is
> comment-based help on the script and every function.

> **Two hazards carried from feature 001**, both recorded in `docs/agent-log.md`:
> `dotnet run` honours `launchSettings.json` and forces Development unless
> `--no-launch-profile` is passed; and `--no-build` reuses stale assemblies, which has
> already produced two false passes in this repository.

---

## Phase 1: Setup

**Purpose**: The inputs and the shell everything else fills in.

- [ ] T001 Create `scripts/Initialize-LocalDb.ps1` as a skeleton with comment-based help, the `-Reset` and `-SkipSeed` switches, and `$ErrorActionPreference = 'Stop'`. Steps are stubs that report and exit 0, so the step format in [contracts/bootstrap-cli.md](./contracts/bootstrap-cli.md) is settled before any logic depends on it
- [ ] T002 [P] Create `src/LexTime.Infrastructure/Seeding/SeedOptions.cs` with the volumes, `ReferenceDate`, `RandomSeed` and share constants from [data-model.md](./data-model.md). `ReferenceDate` is **2026-08-13** and is not free to change — feature 001's `WorkDateConstraintTests.AcceptsWorkDateAtTheOldestSeededBoundary` asserts `2024-08-13` is the far edge of what this feature seeds, and a 24-month window lands exactly there
- [ ] T003 [P] Create `src/LexTime.Api/Maintenance/ExitCodes.cs` with the codes in [contracts/host-cli.md](./contracts/host-cli.md) (0 ok, 1 config, 2 unreachable, 3 failed, 4 band miss, 5 not empty). Distinct codes exist so the script acts on them instead of parsing messages

**Checkpoint**: `dotnet build --warnaserror` clean; the script runs and prints its steps.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The command-line surface and the state inspection every story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T004 Add a verb-dispatch branch to `src/LexTime.Api/Program.cs` between `builder.Build()` and `RunAsync()`. **The no-argument path must be byte-for-byte unchanged in behaviour** — all 21 tests from feature 001 host the application through `WebApplicationFactory` with no arguments and are the regression check for this task
- [ ] T005 Create `src/LexTime.Api/Maintenance/MaintenanceCommands.cs` dispatching the six verbs from [contracts/host-cli.md](./contracts/host-cli.md), resolving services from the built host, catching configuration and connectivity failures and mapping them to exit codes 1 and 2
- [ ] T006 [P] Create `src/LexTime.Infrastructure/Maintenance/MigrationRunner.cs` applying pending migrations in-process via `MigrateAsync()`, with a `reset` path that drops and recreates the database first. This is research.md R0 — it removes the `dotnet-ef` global tool from the quickstart, which is the difference between P18 being satisfied and approximately satisfied
- [ ] T007 [P] Create `src/LexTime.Infrastructure/Maintenance/DatabaseStateInspector.cs` returning `Empty`, `Complete` or `Partial` by comparing per-table row counts against `SeedOptions` (research.md R6). "Any rows at all" is not the test — a seed interrupted midway looks complete and would be reported on faithfully by feature 003 while being wrong
- [ ] T008 Wire the `migrate` and `state` verbs in `src/LexTime.Api/Maintenance/MaintenanceCommands.cs` and register the new services in `src/LexTime.Infrastructure/DependencyInjection.cs` (P21)
- [ ] T009 Run `dotnet test` and confirm all 21 existing tests still pass. If the no-argument host path changed, this is where it surfaces

**Checkpoint**: `dotnet run --project src/LexTime.Api --no-launch-profile -- state` reports
the database state; `-- migrate` applies migrations without `dotnet-ef` installed.

---

## Phase 3: User Story 1 - A reviewer runs the project from a cold machine (Priority: P1) 🎯 MVP

**Goal**: Two commands on a machine with only Docker and the SDK produce a running service
and a populated database, with a usable token printed.

**Independent Test**: On a machine with no project state — and with `dotnet-ef` deliberately
uninstalled — run the two documented commands and request `/health`.

### Tests for User Story 1

- [ ] T010 [P] [US1] Create `tests/LexTime.IntegrationTests/MaintenanceVerbTests.cs` asserting that `migrate` against a fresh container brings the schema up, that re-running it is a no-op exiting 0, and that an unknown verb exits non-zero with a message
- [ ] T011 [P] [US1] Add a test to `tests/LexTime.IntegrationTests/MaintenanceVerbTests.cs` asserting `seed --entries 4000` against an empty database loads exactly 4000 entries and that every one satisfies the duration rules — the reduced-volume path the plan's P13 reasoning depends on

### Implementation for User Story 1

- [ ] T012 [US1] Create `src/LexTime.Infrastructure/Seeding/SeedDataGenerator.cs` producing users, clients, matters and entries from `SeedOptions` alone. **No `DateTime.Now`, no `DateTime.UtcNow`, no `Random.Shared`, no `Guid.NewGuid`** anywhere in the path (FR-020, FR-021). It performs no I/O and touches no database, which is what makes it testable without a container
- [ ] T013 [US1] Create `src/LexTime.Infrastructure/Seeding/BulkSeeder.cs` loading generated rows with `SqlBulkCopy`, **one transaction per table** and an **explicit column mapping** — a positional mapping silently loads `UserId` into `MatterId` the first time a column is added. Parent tables land first so database-assigned keys can be resolved for children
- [ ] T014 [US1] Wire the `seed` verb in `src/LexTime.Api/Maintenance/MaintenanceCommands.cs`, refusing with exit code 5 when the database state is not `Empty` (FR-003). The host reports; the script decides whether to reset
- [ ] T015 [US1] Create `src/LexTime.Infrastructure/Maintenance/ProcedureApplier.cs` executing `db/programmability/*.sql` in sorted filename order, treating an **empty directory as success** — that is the only state this feature will ever see, so it is the default path and not an edge case (FR-010, P7)
- [ ] T016 [US1] Suppress **CA2100** at the single `SqlCommand` construction in `src/LexTime.Infrastructure/Maintenance/ProcedureApplier.cs` with an inline justification naming the input as source-controlled. `.editorconfig` sets CA2100 to `error`, so this **fails the build** until done. Record it as the repository's second P24 review item in `docs/agent-log.md`
- [ ] T017 [P] [US1] Create `src/LexTime.Infrastructure/Maintenance/DevelopmentTokenMinter.cs` signing with the configured key and referencing `AuthenticationSetup.SigningAlgorithm` and `AuthenticationSetup.SectionName` rather than restating them, so the printed token cannot drift from the validator. Claims are the minimum the fallback policy needs — nothing implying an authorisation model that does not exist (research.md R5)
- [ ] T018 [US1] Wire the `apply-procedures` and `mint-token` verbs in `src/LexTime.Api/Maintenance/MaintenanceCommands.cs`
- [ ] T019 [US1] Implement prerequisite verification in `scripts/Initialize-LocalDb.ps1` — container tooling responding, pinned SDK resolvable — failing with exit 1 and a message naming which is missing (FR-011)
- [ ] T020 [US1] Implement container start and readiness polling in `scripts/Initialize-LocalDb.ps1`: attempt an actual query on a bounded retry loop until success or deadline, then fail with exit 2 naming the timeout and how long it waited. **A fixed sleep does not satisfy FR-009**
- [ ] T021 [US1] Implement the migrate, apply-procedures and seed steps in `scripts/Initialize-LocalDb.ps1`, invoking the host with **`--no-launch-profile`** and an explicit environment. Without it `dotnet run` forces Development regardless of what the script sets — this made a Production check silently pass during feature 001
- [ ] T022 [US1] Implement the token-printing step in `scripts/Initialize-LocalDb.ps1`, writing the token to stdout at the end of a successful run and **never to a file inside the repository** (FR-025)

**Checkpoint**: `pwsh ./scripts/Initialize-LocalDb.ps1` on a clean machine with `dotnet-ef`
uninstalled produces a seeded database and a token, in under 3 minutes. **This is the MVP.**

---

## Phase 4: User Story 2 - A developer re-runs the bootstrap safely (Priority: P2)

**Goal**: Running the script again neither duplicates data nor fails on existing objects,
and never leaves a half-built database looking finished.

**Independent Test**: Run the script twice and compare row counts and step reporting between
the runs; then interrupt a seed and run again without `-Reset`.

### Tests for User Story 2

- [ ] T023 [P] [US2] Add a test to `tests/LexTime.IntegrationTests/MaintenanceVerbTests.cs` asserting `seed` against an already-seeded database exits 5 and writes nothing (FR-003)
- [ ] T024 [P] [US2] Create `tests/LexTime.IntegrationTests/DatabaseStateTests.cs` asserting the inspector returns `Empty` on a migrated empty database, `Complete` after a full reduced-volume seed, and `Partial` when one table is deliberately short (research.md R6)

### Implementation for User Story 2

- [ ] T025 [US2] Implement the skip path in `scripts/Initialize-LocalDb.ps1`: each step reports whether it acted or skipped, distinguishably (FR-004). A script that reports success identically either way gives a developer no way to tell a working environment from a no-op
- [ ] T026 [US2] Implement `-Reset` in `scripts/Initialize-LocalDb.ps1` — drop and recreate the **database only**, leaving the container running and untouched (FR-006). Full teardown stays `docker compose down -v` rather than being reimplemented here (FR-008)
- [ ] T027 [US2] Ensure `-Reset` never prompts (FR-007) — the explicit switch is the confirmation, and a prompt makes the script unusable unattended
- [ ] T028 [US2] Implement the partial-state guard in `scripts/Initialize-LocalDb.ps1`: on `Partial`, exit non-zero telling the caller a `-Reset` is required. **It must not top up and must not report success** — this is the failure that would otherwise reach feature 003 as a plausible dataset with wrong totals

**Checkpoint**: two consecutive runs leave identical row counts; `-Reset` rebuilds without
restarting the container; an interrupted seed is refused rather than papered over.

---

## Phase 5: User Story 3 - The seeded data supports a believable report (Priority: P3)

**Goal**: The dataset is shaped like real timekeeping activity, and that shape is asserted
rather than assumed.

**Independent Test**: Query the seeded data for each distribution property and confirm it
falls within its band; generate twice from the same inputs and compare row for row.

### Tests for User Story 3

- [ ] T029 [P] [US3] Create `tests/LexTime.IntegrationTests/SeedGeneratorTests.cs` asserting **determinism**: two generations from identical `SeedOptions` produce identical sequences. No database, no container — this is where a regression would actually be introduced, and it is the property least visible by eye (SC-006)
- [ ] T030 [P] [US3] Add distribution assertions to `tests/LexTime.IntegrationTests/SeedGeneratorTests.cs` at 1/100 scale — weekend share under 10%, non-billable 10–25%, top-ten client share at least 50%, inactive 10–15%, zero duration violations, zero dates after the reference date (SC-004, SC-005, SC-007)
- [ ] T031 [P] [US3] Add a test to `tests/LexTime.IntegrationTests/SeedGeneratorTests.cs` asserting at least one inactive client, one inactive matter and one inactive user each carry history — the fixture feature 004's active-matter rule will need, and the case feature 003 must decide about (SC-007)

### Implementation for User Story 3

- [ ] T032 [US3] Implement weekday concentration and client skew in `src/LexTime.Infrastructure/Seeding/SeedDataGenerator.cs` (FR-013, FR-014). Uniform random data produces uniform plans and a rollup nobody would credit (P9)
- [ ] T033 [US3] Implement the non-billable minority and the inactive-with-history rule in `src/LexTime.Infrastructure/Seeding/SeedDataGenerator.cs` (FR-015, FR-016, FR-017)
- [ ] T034 [US3] Ensure generated matter numbers **repeat across clients** in `src/LexTime.Infrastructure/Seeding/SeedDataGenerator.cs`. Globally unique numbers would leave feature 001's composite index unexercised at volume, which is the one modelling error the schema was written to prevent
- [ ] T035 [US3] Create `src/LexTime.Infrastructure/Seeding/SeedVerifier.cs` running the seven aggregate checks in [data-model.md](./data-model.md) at full volume with their bands as declared constants
- [ ] T036 [US3] Wire the `verify-seed` verb in `src/LexTime.Api/Maintenance/MaintenanceCommands.cs`, exiting 4 on any band miss (FR-023)
- [ ] T037 [US3] Implement the verification step output in `scripts/Initialize-LocalDb.ps1`, printing each check's **measured value alongside its band** whether or not it passed. A check that only reports "ok" tells a reader nothing about how close to a boundary the data sits
- [ ] T038 [P] [US3] Add a test to `tests/LexTime.IntegrationTests/DatabaseStateTests.cs` asserting `verify-seed` exits 4 and names the failing check when data is deliberately skewed outside a band

**Checkpoint**: all three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T039 Update the quickstart in `README.md` to **two** commands, remove `dotnet-ef` from the prerequisites, and document `docker compose down -v` as the full teardown (FR-008, FR-026). Verify by uninstalling the tool and running the script
- [ ] T040 [P] Update the status banner in `README.md` — seeding is no longer future work, and the repository now has a populated database on first run
- [ ] T041 [P] Append this feature's entries to `docs/agent-log.md`, including the CA2100 suppression review (P24) and anything the agent got wrong along the way (P16)
- [ ] T042 Run `dotnet build --warnaserror` and resolve every diagnostic. No `#pragma warning disable` beyond the two reviewed CA2100 suppressions, and no `GenerateDocumentationFile=false` anywhere
- [ ] T043 Run `dotnet test` and confirm the 21 tests from feature 001 pass unchanged alongside the new ones
- [ ] T044 Walk [quickstart.md](./quickstart.md) end to end on a machine with no prior project state and confirm all eight scenarios behave as documented, including the interrupted-seed and Docker-stopped failure paths

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**
- **User Story 1 (Phase 3)**: depends on Phase 2
- **User Story 2 (Phase 4)**: depends on Phase 3 — the skip and reset paths need something
  to skip
- **User Story 3 (Phase 5)**: depends on Phase 3 for the generator. The generator tests
  (T029–T031) need no database and can be written as soon as T012 exists
- **Polish (Phase 6)**: depends on all desired stories

### Within each story

- Tests before implementation where the test can be written first — T010, T011, T023,
  T024, T029, T030, T031 all can
- T012 (generator) before T013 (loader); T035 (verifier) before T036 (verb)

### Parallel Opportunities

- **Phase 1**: T002 and T003 touch different files
- **Phase 2**: T006 and T007 are independent
- **Phase 3**: T010 and T011 together; T017 is independent of the seeding chain
- **Phase 4**: T023 and T024 together
- **Phase 5**: T029, T030, T031 and T038 are all test files with no shared state beyond the
  fixture

---

## Parallel Example: Phase 5 tests

```bash
Task: "Determinism assertions in tests/LexTime.IntegrationTests/SeedGeneratorTests.cs"
Task: "Distribution band assertions at 1/100 scale in the same file"
Task: "Inactive-with-history assertions in the same file"
```

These are written together but land in one file, so treat them as one edit if working
sequentially.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1: Setup
2. Phase 2: Foundational — blocks everything
3. Phase 3: User Story 1
4. **Stop and validate**: uninstall `dotnet-ef`, run the two commands on a clean machine,
   confirm a seeded database and a working token

That is the increment that changes what a reviewer experiences: the repository goes from
"three commands and install a tool" to "two commands".

### Incremental delivery

1. Setup + Foundational → the host has a command-line surface
2. + User Story 1 → the two-command quickstart works (**MVP**)
3. + User Story 2 → safe to re-run, and refuses to lie about a partial seed
4. + User Story 3 → the shape is asserted, not assumed
5. Polish → README, agent log, gate, quickstart walkthrough

### If it overruns

Constitution P3 caps a spec at roughly one evening. The cut candidate named in the plan is
**token minting** (T017, T022) — feature 001 already proves the boundary accepts and
rejects, so the token can move to the feature that adds the first real protected endpoint.
Seeding and the script are not cuttable: they are what makes the quickstart two commands.

---

## Notes

- **The branch does not exist yet.** P22 requires one before the first implementation task.
  Preferred ordering is to merge `001-solution-and-schema` into `main` first, then branch
  `002-bootstrap-and-seed` from `main`; branching off 001 works but stacks its commits
- Commit after each task or logical group; spec, plan and implementation stay separate
  commits (P17)
- Every C# task carries its XML documentation as part of the task — the build fails
  otherwise
- Avoid: reading the clock or machine entropy anywhere in generation (T012); a positional
  bulk-copy column mapping (T013); globally unique matter numbers (T034); omitting
  `--no-launch-profile` (T021); using `--no-build` to verify anything (twice burned)
