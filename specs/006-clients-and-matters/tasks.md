---

description: "Task list for feature 006: Clients, Matters and Timekeepers"
---

# Tasks: Clients, Matters and Timekeepers

**Input**: Design documents from `/specs/006-clients-and-matters/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts)

**Tests**: **Required, and deliberately uneven.** Constitution P13 governs this feature: "trivial
CRUD gets one happy-path and one 404 test each". Three things get more than that — the two
collision paths, the deactivation boundary, and the assertion that timekeepers cannot be written.
Everything else gets the minimum, on purpose.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1, US2, US3 from [spec.md](./spec.md)
- Paths are repository-relative from `E:\LexTime`

**Two rules govern the whole list.** No uniqueness rule may be re-implemented in application
code — the database owns both, and this feature translates their refusals (R2). And **nothing
here may change what features 003, 004 or 005 do**: if a rollup expectation or a rule test has to
be edited to make this feature pass, that is a defect here, not an update.

---

## Phase 1: Setup (Shared Types)

**Purpose**: The shapes everything else is expressed in. No behaviour.

- [X] T001 Create `src/LexTime.Application/Parties/PartyDtos.cs` with `ClientDto`, `MatterDto` and `TimekeeperDto`, each with its `ToDto()` extension beside it (P4). `TimekeeperDto` carries the current rate — it is the only place the API exposes it, and it is the value feature 005's rule 6 captures onto an entry at the moment it is recorded
- [X] T002 [P] Create `src/LexTime.Application/Parties/PartyCommands.cs` with the four commands and three queries from [data-model.md](./data-model.md). **The absent fields are the enforcement**: no code on `ReviseClientCommand`, no number or client on `ReviseMatterCommand`, no active flag on either create. A command that cannot carry a field cannot silently discard it (R4)
- [X] T003 [P] Create `src/LexTime.Application/Parties/PartyWriteResult.cs` with `Succeeded`, `Conflict` and `NotFound`. `Conflict` carries which field collided and the value — three outcomes for three different mistakes, each with a different fix, and collapsing them would make a caller parse prose to learn whether to choose another code or create the parent first

**Checkpoint**: the shapes compile; nothing does anything yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The three ports, their implementations, and the one piece of this feature that is
not boilerplate — turning a constraint violation into an answer.

**⚠️ CRITICAL**: no user story work begins until this phase is complete.

- [X] T004 [P] Create `src/LexTime.Application/Parties/IClientStore.cs` — find, add, save, list. Non-generic, one aggregate. The P4 evaluation was made in feature 005's R2 and is not re-argued per port; note on the type that this is the third of four and that the layering charges it on every aggregate (plan.md, P4 note)
- [X] T005 [P] Create `src/LexTime.Application/Parties/IMatterStore.cs` — the same, plus listing by client
- [X] T006 [P] Create `src/LexTime.Application/Parties/ITimekeeperStore.cs` — **read methods only**. No add, no save. The read-only rule is expressed by the port having no way to write, not by a check inside a handler (FR-020)
- [X] T007 Create `src/LexTime.Infrastructure/Parties/UniqueConstraintTranslator.cs` mapping a `SqlException` to which uniqueness rule was broken. Match on **error numbers 2601 and 2627** and on the **index name** `UX_Clients_ClientCode` or `UX_Matters_ClientId_MatterNumber` — both verified against the container in R2. **Match the index name, never the message text**: SQL Server localises its messages, and the index name is a substituted parameter that survives translation. An exception that matches neither must be rethrown, not swallowed
- [X] T008 Create `src/LexTime.Infrastructure/Parties/EfClientStore.cs`. The create path attempts the insert and translates a violation through T007 — **no lookup before the write**. A check-then-insert is a race that does not remove the catch it was meant to avoid (R2)
- [X] T009 Create `src/LexTime.Infrastructure/Parties/EfMatterStore.cs`, the same, listing by client and ordering by identifier (R8)
- [X] T010 [P] Create `src/LexTime.Infrastructure/Parties/EfTimekeeperStore.cs` — reads only
- [X] T011 [P] Register the three stores in `src/LexTime.Infrastructure/DependencyInjection.cs` and add a `Parties` section to `src/LexTime.Application/DependencyInjection.cs` for the handlers that follow

**Checkpoint**: a collision produces a translated result rather than an escaping exception.

---

## Phase 3: User Story 1 — Registering a client and opening its first matter (Priority: P1) 🎯 MVP

**Goal**: A client and matter created through the API, against which time can immediately be
recorded.

**Independent Test**: register, open, record time through feature 005's endpoint, and confirm it
is accepted. Then repeat the registration and read the conflict.

### Tests for User Story 1

- [X] T012 [P] [US1] Create `tests/LexTime.IntegrationTests/ClientEndpointTests.cs` with the happy path — register returns `201` with an identifier, active — and a `404` for fetching one that does not exist. P13's minimum for the ordinary part
- [X] T013 [US1] Add both collision cases to `ClientEndpointTests.cs`: registering a code already held returns `409` naming the field and value, and **registering the same code in a different case also returns `409`**. The second is FR-006 — the collation makes it true today, and this test is what fails if a collation change ever makes it false (R3)
- [X] T014 [P] [US1] Create `tests/LexTime.IntegrationTests/MatterEndpointTests.cs` with the happy path and a `404` for opening a matter under a client that does not exist. **`404`, not `409`** — a missing parent and a taken number are different mistakes with different fixes
- [X] T015 [US1] Add the composite-uniqueness pair to `MatterEndpointTests.cs`: the same number twice under **one** client returns `409`, and the same number under **two different** clients returns `201` both times. **The second assertion is the one that matters** — feature 002's generator restarts matter numbering at `001` for every client, so an implementation reading the rule as global would break the seeded dataset and every report drawn from it, and only this test would catch it
- [X] T016 [US1] Add the chain test: register a client, open a matter under it, and record time against that matter through feature 005's endpoint, asserting `201` at each step. A creation path producing records the write path then refuses would have delivered nothing, so the third call is what proves the first two (FR-003)
- [X] T017 [P] [US1] Add `400` cases for an empty or whitespace-only code, number or name, asserting they are refused **before** any uniqueness question — a malformed request and a conflicting one are different answers (FR-022)

### Implementation for User Story 1

- [X] T018 [US1] Create `src/LexTime.Application/Parties/ClientHandlers.cs` with the register and get use cases, one class each (P4)
- [X] T019 [US1] Create `src/LexTime.Application/Parties/MatterHandlers.cs` with the open and get use cases. Opening resolves the client from the route and returns `NotFound` when it does not exist, before any insert is attempted
- [X] T020 [P] [US1] Create `src/LexTime.Api/Endpoints/ClientEndpoints.cs` exposing `MapClientEndpoints()` (P21) with `POST` and `GET /{clientId}`, returning `201` with a `Location` header, `409` with the problem document from [contracts/client-endpoints.md](./contracts/client-endpoints.md), or `404`
- [X] T021 [US1] Create `src/LexTime.Api/Endpoints/MatterEndpoints.cs` exposing `MapMatterEndpoints()` with `POST /clients/{clientId}/matters` and `GET /matters/{matterId}`. The conflict detail must name **the client** as well as the number, or a caller goes looking for a matter that is not theirs
- [X] T022 [US1] Wire both into `src/LexTime.Api/Program.cs`

**Checkpoint**: clients and matters can be created, collisions answer sensibly, and time can be
billed against what was made. MVP.

---

## Phase 4: User Story 2 — Closing a matter, and time stopping (Priority: P2)

**Goal**: Deactivation that means something, verified from the other side.

**Independent Test**: record time, close the matter through this feature, confirm feature 005
refuses new time and feature 003 still reports the old.

- [X] T023 [US2] Add the revise use cases to `ClientHandlers.cs` and `MatterHandlers.cs`. Neither touches a code or a number; both leave every other record alone
- [X] T024 [US2] Add `PUT` to `ClientEndpoints.cs` and `MatterEndpoints.cs`, returning `200` with the revised record or `404`. **No conflict path on either** — FR-012 makes `409` unreachable here, and adding handling for it would be dead code
- [X] T025 [US2] Create `tests/LexTime.IntegrationTests/DeactivationBoundaryTests.cs`: record time against a matter, close the matter through this feature's endpoint, and assert feature 005's write path now refuses new time **naming the matter**
- [X] T026 [US2] Add the client case: reopen the matter, close its **client** instead, and assert the refusal now names **the client**. Until this feature there was no way to reach that branch — feature 005 built the message and nothing could cause it, and untested reachable code is how a branch quietly stops working (R6)
- [X] T027 [US2] Add the rollup assertion: the entry recorded before the closure still appears in feature 003's weekly rollup afterwards, with the client's figures unchanged. **A plausible wrong implementation that filtered closed matters out of the report would pass every write-path test above and fail only here** (FR-014)
- [X] T028 [P] [US2] Add the no-cascade test: close a client, read its matters back, and assert their own active flags are untouched (FR-013, SC-005). This is what keeps T026's branch reachable at all
- [X] T029 [P] [US2] Add a test asserting a refused update leaves the stored record byte-identical, compared before and after (FR-016)
- [X] T030 [P] [US2] Add tests for reactivation in both directions and for deactivating something already inactive, which must succeed — the caller asked for a state, not a transition (FR-014, FR-015)

**Checkpoint**: closing a matter changes what two other features do, and both are asserted.

---

## Phase 5: User Story 3 — Finding a client, its matters, or a timekeeper (Priority: P3)

**Goal**: The five read endpoints.

**Independent Test**: with the seeded dataset, list clients by status, page them, list one
client's matters, fetch a timekeeper.

- [X] T031 [P] [US3] Add the list use cases to `ClientHandlers.cs` and `MatterHandlers.cs`, clamping the page window with feature 005's defaults and maximum and ordering by identifier (R8)
- [X] T032 [P] [US3] Create `src/LexTime.Application/Parties/TimekeeperHandlers.cs` with the list and get use cases
- [X] T033 [US3] Add the list routes to `ClientEndpoints.cs` and `MatterEndpoints.cs`, and create `src/LexTime.Api/Endpoints/TimekeeperEndpoints.cs` with `GET /users` and `GET /users/{userId}`. **No `POST`, no `PUT`, on either timekeeper route** — the read-only rule is the absence of a route, not a check inside one
- [X] T034 [US3] Create `tests/LexTime.IntegrationTests/PartyListingTests.cs`: the active-only filter excludes inactive clients, three successive pages return every match exactly once, an unfiltered request is bounded by the default page size, and a client's matter list contains only its own
- [X] T035 [P] [US3] Create `tests/LexTime.IntegrationTests/TimekeeperEndpointTests.cs`: list and fetch work and carry the current rate, and **`POST` and `PUT` on both timekeeper routes are not served** (SC-009). An assertion about something not existing is easy to skip, and it is the only thing between "we decided not to" and "we forgot"

**Checkpoint**: all three stories independently functional; the §4 surface is complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 Add the constraint re-proof to `ClientEndpointTests.cs` (SC-011): write a colliding client code **outside the application** through `DirectSql` and assert the database still refuses it. Feature 001 proved the constraint existed; this proves it still bites now that the application answers for it — which is exactly when someone concludes it is redundant (feature 005's SC-010, same reasoning)
- [X] T037 Read `UniqueConstraintTranslator` line by line (P15). It is the one piece of this feature that is not boilerplate and the one that fails silently if it is wrong — a translator that matched too broadly would report a conflict for an unrelated error, and one that matched too narrowly would let a raw failure escape as a `500`
- [X] T038 Security review recorded in `docs/agent-log.md` (P24): confirm every query is EF-parameterised, that all ten routes carry no `AllowAnonymous`, that no route can create or modify a timekeeper, and that identifiers come from the route rather than the body. **A new `CA2100` suppression here is a design error, not a finding to justify**
- [X] T039 [P] Add feature 006 entries to `docs/agent-log.md` for whatever went wrong, with symptom and how it was caught (P16). If nothing did, say so rather than inventing friction
- [X] T040 [P] Update `README.md`: the API surface is now complete at seventeen endpoints, the two positions this feature took (immutable codes, no cascade), and that uniqueness is answered from the database rather than guessed at. Update the status banner and test count
- [X] T041 Run `dotnet build --warnaserror --no-incremental` and confirm `0 Warning(s), 0 Error(s)`. `--no-incremental` because an incremental build has three times in this repository reported a clean result it had not earned
- [X] T042 Run `dotnet test` and confirm green, **with features 003, 004 and 005 passing unchanged**. If a rollup expectation or a rule test had to be edited, that is a defect in this feature
- [X] T043 Walk `quickstart.md` end to end, including Validation 3 — the pair of refusals that shows why deactivation does not cascade, and the rollup check that shows closing a matter erases nothing

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies
- **Phase 2 (Foundational)** — needs Phase 1; **blocks all stories**
- **Phase 3 (US1)** — needs Phase 2
- **Phase 4 (US2)** — needs US1's handlers and endpoint files
- **Phase 5 (US3)** — needs Phase 2 only; independent of US1 and US2
- **Phase 6 (Polish)** — needs the stories being shipped

### Story dependencies

- **US1 (P1)** — independent once Phase 2 is done, and delivers the MVP alone: the surface can
  create the records everything else needs
- **US2 (P2)** — extends US1's handlers and endpoints. Its boundary tests need feature 005's write
  path and feature 003's rollup, both of which already exist
- **US3 (P3)** — reads only, and the first thing the cut order drops

### Within each story

- Tests before implementation
- Ports before stores before handlers before endpoints; no endpoint holds logic
- **T016 must come after T014 and T015**, not with them. It is the chain test, and writing it
  alongside the pieces it chains invites shaping it around what they happen to do

### Parallel opportunities

- **T002, T003** — commands and the result type, two files
- **T004, T005, T006** — three ports, three files
- **T010, T011** — a read-only store and two registration files
- **T012, T014, T017** — three independent test groups, though T013 and T015 add to the files
  T012 and T014 create and follow them
- **T028, T029, T030** — three independent test cases once T024 exists
- **T031, T032** — list handlers and the timekeeper handlers
- **T039, T040** — two documentation files

### The ordering trap worth naming

**T027 must not be folded into T025.** The write-path refusal and the rollup assertion look like
one test about deactivation and are not: the first proves new time is refused, the second proves
old time survives. An implementation that filtered closed matters out of the report would pass
the first and fail only the second, and combining them makes it easy to write only the half that
was on your mind.

---

## Implementation Strategy

### MVP first

1. Phase 1 → Phase 2 → Phase 3
2. **Stop and validate**: register a client, open a matter, bill an hour against it
3. At that point the API can create everything it needs. Closing and listing are additions

### If the evening runs out

The plan fixes the cut order so it is not improvised here:

1. **Cut first**: the two timekeeper endpoints (T032, T035, and their half of T033). Pure reads
   over seeded data with nothing depending on them
2. **Cut second**: `GET /clients/{id}` and `GET /matters/{id}` as separate use cases — both are
   reachable by listing with a filter
3. **Never cut**: T007, T013, T015, T025–T027, T036. The translator, both collision pairs, the
   three boundary assertions and the constraint re-proof. Everything else in this feature is
   boilerplate; those are not

### Commit shape (P17)

Plan and tasks land separately from implementation. Within implementation, commit per phase.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- Every new type, method, parameter and test method carries an XML doc comment (P25)
- **No uniqueness rule is re-implemented in application code.** The database owns both; this
  feature translates their refusals. An application-level duplicate check would be a second copy
  of a rule that would eventually disagree with the first
- **Nothing here may change what features 003, 004 or 005 do.** T042 is the check, and an edited
  expectation elsewhere is a defect here
