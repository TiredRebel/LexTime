---

description: "Task list for feature 001: Solution and Schema"
---

# Tasks: Solution and Schema

**Input**: Design documents from `/specs/001-solution-and-schema/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/health.md](./contracts/health.md)

**Tests**: Included. FR-028 and FR-029 require them explicitly, and constitution P11 fixes
how they run — real SQL Server via Testcontainers, no in-memory provider anywhere.

**Organization**: Grouped by user story so each is independently implementable and
testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)

## Path Conventions

Four source projects under `src/`, one test project under `tests/`, per
constitution P4 and `docs/prd.md` §5.

> **Applies to every code task below**: constitution **P25** requires an XML
> documentation comment on every type, method, property, parameter, return value and
> documented exception — private and internal members included, test methods included.
> `GenerateDocumentationFile` is on from T004 onward, so an undocumented member is a
> build failure, not a review note. A comment that restates its signature does not
> satisfy P25. Budget for this in every task rather than treating it as a cleanup pass.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution skeleton, the build gate, and the container definition. Nothing here
depends on the data model.

- [ ] T001 Create `LexTime.sln` at the repository root with `src/` and `tests/` directories
- [ ] T002 Create the four source projects — `src/LexTime.Api/LexTime.Api.csproj` (web), `src/LexTime.Application/LexTime.Application.csproj` (classlib), `src/LexTime.Domain/LexTime.Domain.csproj` (classlib), `src/LexTime.Infrastructure/LexTime.Infrastructure.csproj` (classlib) — and add all four to the solution
- [ ] T003 Create `tests/LexTime.IntegrationTests/LexTime.IntegrationTests.csproj` (xUnit) and add it to the solution
- [ ] T004 Create `Directory.Build.props` at the repository root setting `AnalysisMode=Recommended`, `AnalysisModeSecurity=All`, `EnforceCodeStyleInBuild=true`, `Nullable=enable`, `NuGetAudit=true`, `NuGetAuditMode=all` and `GenerateDocumentationFile=true`. No project may opt out (FR-005)
- [ ] T005 [P] Create `.editorconfig` at the repository root for per-rule severity tuning
- [ ] T006 Add project references establishing the dependency direction — `Api` → `Application` and `Api` → `Infrastructure` (composition only), `Application` → `Domain`, `Infrastructure` → `Domain`. `LexTime.Domain` must reference no project and no persistence, web or serialisation package (FR-002)
- [ ] T007 [P] Add a `<Description>` element to each of the five `.csproj` files stating that project's purpose (FR-003, P25 module level)
- [ ] T008 [P] Create `docker-compose.yml` at the repository root running SQL Server 2022 with a named persistent volume and a container health check (FR-017)
- [ ] T009 [P] Create `db/programmability/.gitkeep` so the empty procedure directory is committed (FR-018)
- [ ] T010 [P] Create `src/LexTime.Api/appsettings.Development.json` with the database connection string and the symmetric development signing key. The key must not be hard-coded in source (FR-021)
- [ ] T011 Verify the documentation gate fires — add a public method with no `<summary>` to `tests/LexTime.IntegrationTests/`, confirm `dotnet build --warnaserror` fails with **CS1591**, then remove it (SC-003)

**Checkpoint**: `dotnet build --warnaserror` succeeds on an empty solution, and T011 has
proved the gate is actually wired up rather than assumed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The data model, the schema and the test harness. Every user story depends on
some part of this.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T012 [P] Create `src/LexTime.Domain/Entities/User.cs` — `UserId`, `Email`, `FullName`, `DefaultHourlyRate`, `IsActive`, `CreatedAtUtc` per [data-model.md](./data-model.md)
- [ ] T013 [P] Create `src/LexTime.Domain/Entities/Client.cs` — `ClientId`, `ClientCode`, `Name`, `IsActive`, `CreatedAtUtc`
- [ ] T014 [P] Create `src/LexTime.Domain/Entities/Matter.cs` — `MatterId`, `ClientId`, `MatterNumber`, `Name`, `IsBillableByDefault`, `IsActive`, `CreatedAtUtc`
- [ ] T015 [P] Create `src/LexTime.Domain/Entities/TimeEntry.cs` — `TimeEntryId`, `UserId`, `MatterId`, `WorkDate`, `DurationMinutes`, `IsBillable`, `HourlyRateSnapshot`, `Narrative`, `CreatedAtUtc`, `UpdatedAtUtc`
- [ ] T016 Create `src/LexTime.Infrastructure/Persistence/LexTimeDbContext.cs` with the four `DbSet` properties and configuration discovery (depends on T012–T015)
- [ ] T017 [P] Create `src/LexTime.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — types per `docs/prd.md` §3, unique index on `Email` (FR-010)
- [ ] T018 [P] Create `src/LexTime.Infrastructure/Persistence/Configurations/ClientConfiguration.cs` — unique index on `ClientCode` (FR-010)
- [ ] T019 [P] Create `src/LexTime.Infrastructure/Persistence/Configurations/MatterConfiguration.cs` — FK to `Clients`, and a **composite** unique index on (`ClientId`, `MatterNumber`), not a global unique index on `MatterNumber` (FR-010). Two clients may each have a matter numbered `001`; this is the most likely modelling error in the feature
- [ ] T020 [P] Create `src/LexTime.Infrastructure/Persistence/Configurations/TimeEntryConfiguration.cs` — FKs to `Users` and `Matters`, and the check constraint `DurationMinutes > 0 AND DurationMinutes % 6 = 0 AND DurationMinutes <= 1440` (FR-011). **Do not add any constraint on `WorkDate`** (FR-012) — see the warning below
- [ ] T021 Create `src/LexTime.Infrastructure/DependencyInjection.cs` exposing `AddLexTimeInfrastructure()`, registering the `DbContext` from configuration (FR-004, P21)
- [ ] T022 [P] Create `src/LexTime.Application/DependencyInjection.cs` exposing `AddLexTimeApplication()`. It registers nothing in this feature; the project exists because P4 requires it and the reporting interface lands in it with feature 003
- [ ] T023 Generate the initial EF Core migration into `src/LexTime.Infrastructure/Migrations/`, then **read the generated SQL line by line** before committing (constitution P15). Confirm: four tables, the duration check constraint present, the composite matter index present, no index on (`WorkDate`, `IsBillable`) (FR-014), no constraint on `WorkDate` (FR-012)
- [ ] T024 Create `tests/LexTime.IntegrationTests/SqlServerFixture.cs` — a Testcontainers fixture starting the SQL Server 2022 image, applying migrations, and exposing a connection string. No in-memory or file-based provider may be referenced anywhere in this project (FR-028, P11)
- [ ] T025 Create `tests/LexTime.IntegrationTests/DatabaseCollection.cs` so the container is shared across test classes rather than started per class

> **T020 and T023 are the pair most easily got wrong.** FR-012 requires that something be
> *absent*. A `WorkDate` constraint looks like an obvious omission to anyone reading the
> model later, and adding it would silently break feature 002's seed — which writes 24
> months of history, most of it older than 90 days. T033 is the test that makes that
> breakage loud.

**Checkpoint**: `dotnet ef database update` produces four tables with constraints against
a running container, and re-running it changes nothing (SC-007).

---

## Phase 3: User Story 1 - The solution builds clean and runs (Priority: P1) 🎯 MVP

**Goal**: A developer can bring up the database, apply the schema, start the service, and
get a green health check — with a build that produces zero warnings.

**Independent Test**: On a machine with container tooling and the pinned SDK, run
`docker compose up -d`, apply the migration, start the API and request `/health`. Delivers
a working, verifiably clean skeleton on its own.

### Tests for User Story 1

- [ ] T026 [P] [US1] Write `tests/LexTime.IntegrationTests/HealthEndpointTests.cs` asserting that with the database reachable, `GET /health` returns **200**, `status: Healthy`, and a `checks[]` array containing an entry named `database` (FR-023, FR-024, FR-025)
- [ ] T027 [P] [US1] Add a test to `HealthEndpointTests.cs` asserting that with the database unreachable, `GET /health` returns **503**, `status: Unhealthy`, and the `database` check named as failing with a description. This is the test that catches a check which constructs a connection without executing a query (FR-026)

### Implementation for User Story 1

- [ ] T028 [US1] Register a named `database` health check in `src/LexTime.Api/HealthChecks/` that **executes a trivial query** rather than only opening a connection, with connection and command timeouts short enough that a failure returns inside 5 seconds (FR-026, SC-004)
- [ ] T029 [US1] Create `src/LexTime.Api/HealthChecks/HealthResponseWriter.cs` emitting the JSON body in [contracts/health.md](./contracts/health.md) — overall status, total duration, and per-check name, status, duration and description. The framework default writer returns a bare status string and does not meet the contract (FR-025)
- [ ] T030 [US1] Ensure the health response discloses no connection string, credentials, server hostname or stack trace. The endpoint is unauthenticated; anything it returns is public (FR-027)
- [ ] T031 [US1] Wire `src/LexTime.Api/Program.cs` — `AddLexTimeApplication()`, `AddLexTimeInfrastructure()`, health checks, Swagger, and map `/health` as anonymous (FR-004, FR-019)

**Checkpoint**: `dotnet build --warnaserror` is clean, the service serves Swagger, and
`/health` returns 200 with the database check named. **This is the MVP.**

---

## Phase 4: User Story 2 - The database enforces its own rules (Priority: P2)

**Goal**: The rules expressible as storage constraints are enforced by the database
itself, so data written by any route — application, script, or a query window — is held to
them.

**Independent Test**: Attempt each violating insert directly against the database with no
application involved, and confirm rejection.

The constraints themselves were created in Phase 2 (T017–T020, T023). This phase is the
verification that they behave as specified — which is where the value is, since a
constraint nobody tested is a constraint nobody knows exists.

### Tests for User Story 2

- [ ] T032 [P] [US2] Create `tests/LexTime.IntegrationTests/DurationConstraintTests.cs` asserting that direct inserts with duration `7` (not a multiple of six), `0`, `-6` and `1446` are each rejected by the database, and that `6` is accepted (FR-011, SC-005)
- [ ] T033 [P] [US2] Create `tests/LexTime.IntegrationTests/WorkDateConstraintTests.cs` asserting that a direct insert dated three years in the past is **accepted**. This is a positive test that a constraint is absent (FR-012) — without it, someone "fixes" the missing date constraint and feature 002's seed silently fails to load
- [ ] T034 [P] [US2] Create `tests/LexTime.IntegrationTests/UniquenessConstraintTests.cs` asserting: a duplicate `ClientCode` is rejected; a duplicate `Email` is rejected; matter number `001` is **accepted** under two different clients; matter number `001` is rejected twice under one client (FR-010, SC-005)

All three test classes must insert directly against the database rather than through EF
Core entity tracking where practical — constitution P6 requires the database to hold this
line independently, and feature 002's bulk load will not pass through application
validation.

**Checkpoint**: Storage constraints proven independently of application code.

---

## Phase 5: User Story 3 - Protected surface is closed by default (Priority: P3)

**Goal**: A caller without a valid token reaches the health check and the API
documentation and nothing else.

**Independent Test**: Call the health endpoint and a placeholder protected route with and
without a valid token, and compare responses.

### Tests for User Story 3

- [ ] T035 [P] [US3] Create `tests/LexTime.IntegrationTests/TokenFactory.cs` minting valid, expired and wrongly-signed tokens with the development key, so the boundary can be exercised before any script mints one (feature 002 adds the reviewer-facing token)
- [ ] T036 [P] [US3] Create `tests/LexTime.IntegrationTests/AuthBoundaryTests.cs` asserting: no token → **401**; malformed token → 401; expired token → 401; wrongly-signed token → 401; **valid token → 200** (FR-019, FR-020, SC-006). The success case matters as much as the failures — without it, a route that rejects everything unconditionally passes

### Implementation for User Story 3

- [ ] T037 [US3] Configure JWT bearer validation in `src/LexTime.Api/Program.cs` — signing key from configuration, **restricted signing algorithm** rather than one inferred from the token header, and lifetime and audience validation explicitly enabled (FR-020, FR-021)
- [ ] T038 [US3] Require authorisation by default across the app, with `/health` and the Swagger UI explicitly anonymous (FR-019)
- [ ] T039 [US3] Add a temporary placeholder protected route `GET /api/v1/ping` in `src/LexTime.Api/` that exists only to prove the boundary rejects and accepts. It is removed when the first real endpoint lands and is **not** one of the seventeen in `docs/prd.md` §4 (research.md R5)

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T040 [P] Create `docs/agent-log.md` and record what the agent got wrong during this feature — what it generated, the symptom, and how it was caught (constitution P16). The file is first referenced by this feature's work, so it is created here
- [ ] T041 [P] Update the quickstart section of `README.md` to describe the **three** manual commands this feature actually delivers, not the two-command script that arrives with feature 002. Claiming the two-command quickstart before it exists is the P18 violation; describing the current state truthfully is not
- [ ] T042 Run a manual security review of the JWT configuration in `src/LexTime.Api/Program.cs` before committing it — key source, algorithm restriction, lifetime and audience validation (constitution P24). Record findings, or the reason they were accepted, in `docs/agent-log.md`
- [ ] T043 Run `dotnet build --warnaserror` across the solution and resolve every analyzer diagnostic and every CS1591. No `#pragma warning disable` and no `GenerateDocumentationFile=false` — if a suppression is genuinely warranted it carries a justification and becomes a P24 review item
- [ ] T044 Walk [quickstart.md](./quickstart.md) end to end on a machine with no prior project state and confirm all eight scenarios behave as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **blocks all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2. Needs the `DbContext` for the health check
- **User Story 2 (Phase 4)**: Depends on Phase 2. Needs the migration applied
- **User Story 3 (Phase 5)**: Depends on Phase 2 for the test harness only — it does not
  touch the data model, so it can run in parallel with Phase 4
- **Polish (Phase 6)**: Depends on all desired stories

### Within each story

- Tests before implementation where the test can be written first — T026, T027, T035 and
  T036 all can
- Phase 4 is tests only; its implementation happened in Phase 2

### Parallel Opportunities

- **Phase 1**: T005, T007, T008, T009, T010 all touch different files
- **Phase 2**: T012–T015 (four entities), then T017–T020 (four configurations) once T016
  exists
- **Phase 3**: T026 and T027 can be written together
- **Phase 4**: T032, T033 and T034 are three separate files with no shared state beyond
  the fixture
- **Phase 5**: T035 and T036 together; Phase 5 can run alongside Phase 4 entirely

---

## Parallel Example: Phase 2 entities

```bash
Task: "Create src/LexTime.Domain/Entities/User.cs"
Task: "Create src/LexTime.Domain/Entities/Client.cs"
Task: "Create src/LexTime.Domain/Entities/Matter.cs"
Task: "Create src/LexTime.Domain/Entities/TimeEntry.cs"
```

Then, once `LexTimeDbContext` exists:

```bash
Task: "Create Persistence/Configurations/UserConfiguration.cs"
Task: "Create Persistence/Configurations/ClientConfiguration.cs"
Task: "Create Persistence/Configurations/MatterConfiguration.cs"
Task: "Create Persistence/Configurations/TimeEntryConfiguration.cs"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1: Setup — through T011, including seeing the documentation gate fail once
2. Phase 2: Foundational — blocks everything
3. Phase 3: User Story 1
4. **Stop and validate**: clean build, service runs, `/health` green

That is a demonstrable increment: a layered solution with a real schema and a health
endpoint that tells the truth.

### Incremental delivery

1. Setup + Foundational → foundation ready
2. + User Story 1 → the MVP
3. + User Story 2 → constraints proven independently of application code
4. + User Story 3 → boundary proven to both reject and accept
5. Polish → agent log, honest README, security review, quickstart walkthrough

### If it overruns

Constitution P3 caps a spec at roughly one evening, and this feature exists because the
original was split for exceeding it. If Phase 2 runs long, the cut candidate is **Phase 5**
— the auth boundary can move to feature 004, which adds the first real protected endpoint
anyway. Phases 1 to 4 are not cuttable: without the schema and its constraints, feature
002 has nothing to seed into.

---

## Notes

- Commit after each task or logical group. Spec, plan and implementation are separate
  commits (P17), and work stays on `001-solution-and-schema` (P22)
- Every code task carries its XML documentation comments as part of the task, not as a
  cleanup pass — the build fails otherwise
- T023 requires reading generated SQL manually before committing (P15). An agent that
  writes both the model and its migration will happily agree with itself
- Avoid: adding a `WorkDate` constraint (T020, T033), a global unique index on
  `MatterNumber` (T019), the covering index (T023), or any in-memory provider in the test
  project (T024)
