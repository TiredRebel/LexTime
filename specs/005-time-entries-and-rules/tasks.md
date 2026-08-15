---

description: "Task list for feature 005: Time Entries and the Domain Rules"
---

# Tasks: Time Entries and the Domain Rules

**Input**: Design documents from `/specs/005-time-entries-and-rules/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts)

**Tests**: **Required.** `docs/prd.md` §6.4 makes twelve rule tests a done criterion — every rule
refusing and every rule accepting — and SC-001 forbids any rule being covered by only one of the
two. The rule tests are the deliverable, not a check on it.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1, US2, US3 from [spec.md](./spec.md)
- Paths are repository-relative from `E:\LexTime`

**One rule governs the whole list**: no rule may be expressed anywhere but `TimeEntryRuleSet`
(FR-011). A rule that appears in a handler or an endpoint is a defect even when it agrees with
the one in the domain — because the two will eventually stop agreeing and nothing will say so.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The clock, and the folders everything else lands in.

- [ ] T001 Create `tests/LexTime.IntegrationTests/FixedClock.cs` — a `TimeProvider` subclass overriding `GetUtcNow()` to return a value supplied at construction. Five lines and no package: `Microsoft.Extensions.TimeProvider.Testing` would supply `FakeTimeProvider`, and adding a dependency to a repository whose quality argument is "you need nothing installed" is a poor trade for one override (R3)
- [ ] T002 [P] Register `TimeProvider.System` as a singleton in `src/LexTime.Api/Program.cs` so handlers can take the clock by injection. Rule 4 is a rule about today, and a rule that reads the clock itself cannot be tested without waiting for the calendar

**Checkpoint**: a test can fix "today" to any date; the application uses the real one.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The types the rules are expressed over, the port to storage, and the plumbing from a
violation to an HTTP response. No rule logic yet — that is user story 1.

**⚠️ CRITICAL**: no user story work begins until this phase is complete.

- [ ] T003 [P] Create `src/LexTime.Domain/Rules/DomainRule.cs` enumerating the six rules of `docs/prd.md` §2.1 plus `ActiveTimekeeper` from FR-013, named as in [data-model.md](./data-model.md). Document on `RateSnapshot` that it cannot be violated by a submission — it is a rule about what the system does, enforced structurally by the revise command having no rate field
- [ ] T004 [P] Create `src/LexTime.Domain/Rules/TimeEntryFacts.cs` — the record in [data-model.md](./data-model.md). Document `OtherMinutesOnDate` as *excluding the entry being revised*, and `EvaluateWorkDate`/`EvaluateMatter` as the field-scoping the clarification decided. `Today` is a field, never a clock call
- [ ] T005 [P] Create `src/LexTime.Domain/Rules/RuleViolation.cs` carrying the rule, the offending value and a sentence naming both the value and the limit. Returned, never thrown: a refused submission is an ordinary outcome of a well-formed request, and throwing would push rule handling into middleware where it is invisible (R6)
- [ ] T006 Create `src/LexTime.Domain/Rules/TimeEntryRuleSet.cs` as a **stub that returns no violations**. This fixes the signature before anything binds to it and makes the tests in Phase 3 fail by accepting everything — which is the informative red, rather than "type not found"
- [ ] T007 Create `src/LexTime.Application/TimeEntries/ITimeEntryStore.cs` — the port. Non-generic, one aggregate, with `FindAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`, `ListAsync` and `SumMinutesForUserOnDateAsync`. Document the P4 evaluation from [research.md](./research.md) R2 on the type itself: this is the interface a reviewer will check against P4's repository ban, and the reasoning belongs where they will look
- [ ] T008 [P] Create `src/LexTime.Application/TimeEntries/TimeEntryDto.cs` with its `ToDto()` extension beside it (P4). Include the captured rate — a caller must not have to re-fetch to learn what was recorded
- [ ] T009 Create `src/LexTime.Infrastructure/TimeEntries/EfTimeEntryStore.cs` implementing T007 with EF Core. `SumMinutesForUserOnDateAsync` takes an optional entry to exclude, which is what lets a duration be *reduced* on a day already at the limit. Expose a way for a caller to run the sum and the write inside one serialisable transaction (R4)
- [ ] T010 [P] Create `src/LexTime.Api/Endpoints/RuleViolationResults.cs` mapping violations to `400` `application/problem+json`, with the rule name and offending value in the extension members. **All violated rules, not just the first** — a submission wrong in three ways should not take three round trips (R6)
- [ ] T011 [P] Register the store in `src/LexTime.Infrastructure/DependencyInjection.cs` and add a `TimeEntries` registration section to `src/LexTime.Application/DependencyInjection.cs` for the handlers that follow

**Checkpoint**: the types compile, the store round-trips an entry, and no rule is enforced yet.

---

## Phase 3: User Story 1 — Recording time, and refusing a bad entry (Priority: P1) 🎯 MVP

**Goal**: All six rules enforced, in one place, reached by the record path.

**Independent Test**: submit one conforming entry and one violating entry per rule against the
seeded database, and read what comes back.

### Tests for User Story 1

> Write these first. Against T006's stub they fail by **accepting** everything, which is the
> useful red — it proves the test exercises the rule rather than a missing type.

- [ ] T012 [P] [US1] Create `tests/LexTime.IntegrationTests/TimeEntryRuleTests.cs` covering rules 1 and 2: refusing `0`, `-6`, `7`, `1446`; accepting `6`, `1440`. **Rule 2's boundary is `1446`, not `1441`** — `1441` proves nothing about the 24-hour maximum because rule 1 refuses it first, and the first value that isolates rule 2 is the next legal increment above the limit ([contracts/domain-rules.md](./contracts/domain-rules.md))
- [ ] T013 [US1] Add rule 3 cases to `TimeEntryRuleTests.cs`: 1,400 already recorded plus 60 refused, plus 40 accepted, a total of exactly 1,440 accepted. **Include the reduction case**: an entry of 600 on a day totalling 1,440 reduced to 300 must be accepted. An implementation counting the entry against itself refuses that, and no test of an *increase* would notice
- [ ] T014 [US1] Add rule 4 cases: tomorrow refused, 91 days ago refused, today accepted, exactly 90 days ago accepted. **Every date computed relative to the fixed clock**, never a literal — a test asserting `2026-06-01` is inside the window passes today and fails in December, and a suite that rots on a date fails while nothing is wrong (FR-026, SC-009)
- [ ] T015 [P] [US1] Add rule 5 and FR-013 cases: an inactive matter refused, an active matter of an inactive client refused, an inactive timekeeper refused, all three active accepted. Assert the refusal **says which of the two was inactive** — a caller told only "not active" cannot tell whether to reopen a matter or a client (FR-008)
- [ ] T016 [P] [US1] Add a test asserting that a submission breaking three rules returns three violations in a stable order, so a caller can fix them in one pass

### Implementation for User Story 1

- [ ] T017 [US1] Implement rules 1, 2 and 3 in `src/LexTime.Domain/Rules/TimeEntryRuleSet.cs`. Rule 3 compares `OtherMinutesOnDate + DurationMinutes` against the maximum — the *other* in that field name is what makes a reduction possible
- [ ] T018 [US1] Implement rules 4, 5 and the timekeeper check in `TimeEntryRuleSet.cs`, honouring `EvaluateWorkDate` and `EvaluateMatter`. Rule 5 reports which of matter and client was inactive; the two flags are separate on the facts for exactly this reason
- [ ] T019 [P] [US1] Create `src/LexTime.Application/TimeEntries/RecordTimeEntryCommand.cs` — timekeeper, matter, work date, duration, billable flag, narrative. **No rate field**: a caller able to state the rate could bill at any figure they liked, and rule 6 would be decoration
- [ ] T020 [US1] Create `src/LexTime.Application/TimeEntries/RecordTimeEntryHandler.cs` — gather the facts, evaluate the rules, and on success capture the timekeeper's current rate onto the entry and persist. The gather-evaluate-persist sequence runs inside the serialisable transaction from T009, or rule 3 is defeatable by two concurrent submissions (R4)
- [ ] T021 [US1] Create `src/LexTime.Api/Endpoints/TimeEntryEndpoints.cs` exposing `MapTimeEntryEndpoints()` (P21) with `POST /api/v1/time-entries`, returning `201` with a `Location` header, or the problem response from T010. Wire it in `src/LexTime.Api/Program.cs`
- [ ] T022 [US1] Create `tests/LexTime.IntegrationTests/TimeEntryWriteTests.cs` with the record path over HTTP: a valid submission returns `201` with a `hourlyRateSnapshot` the caller did not send, and a violating submission returns `400` naming the rule. This tier asks *is the rule reached*; T012–T016 ask *is the rule right*, and a feature enforcing every rule in a class nothing called would pass those completely (R10)
- [ ] T023 [US1] Add a concurrency test: two records submitted simultaneously for the same timekeeper and date that individually pass and together exceed the daily maximum. Exactly one must succeed. Without the serialisable transaction both pass and the stored total exceeds the limit, with nothing failing
- [ ] T024 [US1] Add a test asserting an entry recorded through the API is visible to the rollup for its week. The rollup is not modified here, but an entry the write path accepts and the report cannot see would be a defect in this feature — the two halves must agree about what a time entry is

**Checkpoint**: time can be recorded, bad data cannot enter, and every rule is proved both ways. MVP.

---

## Phase 4: User Story 2 — Correcting an entry (Priority: P2)

**Goal**: Revise and delete, with the field-scoped rules the clarification settled.

**Independent Test**: record an entry, change each field in turn, confirm the rules apply to the
result; then remove it.

- [ ] T025 [P] [US2] Create `src/LexTime.Application/TimeEntries/ReviseTimeEntryCommand.cs` — matter, work date, duration, billable flag, narrative. **No timekeeper and no rate.** Moving an entry between timekeepers would change whose daily total it counts against and whose rate it should have captured, and neither has a defined answer; the rate is rule 6, and omitting the field means the mistake cannot be made through the API (R8)
- [ ] T026 [US2] Create `src/LexTime.Application/TimeEntries/ReviseTimeEntryHandler.cs`. Load the stored entry, compare field by field, and set `EvaluateWorkDate` only when the submitted date differs and `EvaluateMatter` only when the matter differs (R5). Carry the stored rate forward untouched. Exclude this entry from the daily total
- [ ] T027 [P] [US2] Create `src/LexTime.Application/TimeEntries/DeleteTimeEntryHandler.cs`. No rule gates deletion — `docs/prd.md` §2.2 rules out a locking workflow, and gating removal on a period rule would be the first half of one (FR-017)
- [ ] T028 [US2] Add `PUT` and `DELETE` to `TimeEntryEndpoints.cs`, returning `200` with the revised entry, `204` on delete, `404` for an identifier matching nothing, and the problem response for a violation
- [ ] T029 [US2] Add revision tests to `TimeEntryWriteTests.cs`: an entry older than 90 days accepts a narrative-only change, and refuses the same request with its work date moved by one day. **That pair is the clarification made observable** — an old entry can be corrected but not re-dated
- [ ] T030 [P] [US2] Add a test asserting a refused revision leaves the stored row byte-identical, compared before and after. A partially applied update is worse than a refused one (FR-015)
- [ ] T031 [P] [US2] Add the rule 6 acceptance test: record an entry, change the timekeeper's rate, revise the entry's narrative, assert the captured rate is unchanged. This is the test that catches a revise handler which rebuilt the entity and re-read the current rate — a mistake that would otherwise rewrite history on every edit, silently
- [ ] T032 [P] [US2] Add a delete test: the entry is gone from listings and from the rollup, and a second delete returns `404`

**Checkpoint**: entries can be corrected and removed, and the rules follow the correction.

---

## Phase 5: User Story 3 — Finding entries (Priority: P3)

**Goal**: List with filters and paging, and fetch one.

**Independent Test**: with the seeded dataset, request with each filter alone and combined, and
page through a filtered result.

- [ ] T033 [P] [US3] Create `src/LexTime.Application/TimeEntries/ListTimeEntriesQuery.cs` — optional timekeeper, matter, from and to, plus skip and take with the defaults and bounds in [contracts/time-entry-endpoints.md](./contracts/time-entry-endpoints.md)
- [ ] T034 [US3] Create `src/LexTime.Application/TimeEntries/ListTimeEntriesHandler.cs` returning the page and the total matching the filters. **Ordered by identifier, not by work date** — the seed has thousands of entries per date, so date alone is not a total order and successive pages can drop one row and repeat another (R9)
- [ ] T035 [P] [US3] Create `src/LexTime.Application/TimeEntries/GetTimeEntryHandler.cs` returning one entry with its captured rate, or nothing when the identifier matches none
- [ ] T036 [US3] Add both `GET` routes to `TimeEntryEndpoints.cs`, bounding `take` rather than honouring it literally and treating a negative `skip` as zero
- [ ] T037 [US3] Create `tests/LexTime.IntegrationTests/TimeEntryListingTests.cs`: each filter alone, two combined, and three successive pages in which every entry appears exactly once. Also assert an unfiltered request is bounded by the default page size rather than returning the whole table

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T038 Add the storage-constraint re-proof to `TimeEntryWriteTests.cs` (SC-010): write a violating duration **outside the application** through `DirectSql` and assert the database still refuses it. Feature 001 proved the constraint existed; this proves it still bites now that a second enforcement layer arrived — which is exactly when someone concludes it is redundant and deletes it (R7)
- [ ] T039 Read the transaction and isolation handling in `EfTimeEntryStore` line by line (P15). Little SQL is written here, but the serialisable scope is the part that is subtly wrong if the read and the write end up in different transactions — and no test of a single request would notice
- [ ] T040 Security review recorded in `docs/agent-log.md` (P24): confirm every query is EF-parameterised with nothing concatenated, that the five routes carry no `AllowAnonymous`, and that a caller cannot set the rate, the timekeeper on an update, or an identifier. **A new `CA2100` suppression here is a design error, not a finding to justify**
- [ ] T041 [P] Add feature 005 entries to `docs/agent-log.md` for whatever went wrong, with symptom and how it was caught (P16). If nothing did, say so rather than inventing friction
- [ ] T042 [P] Add a domain-rules section to `README.md`: the six rules, that they live in one place, the two positions this feature took (field-scoped rules on update, and no ownership model), and that rule 6 is enforced by the absence of a field rather than by a check. Update the status banner
- [ ] T043 Run `dotnet build --warnaserror --no-incremental` and confirm `0 Warning(s), 0 Error(s)`. `--no-incremental` because an incremental build has previously reported a clean gate that a full build did not
- [ ] T044 Run `dotnet test` and confirm green, with features 003 and 004 tests passing unchanged — the rollup's hand-computed expectations and the covering-index equivalence both untouched
- [ ] T045 Walk `quickstart.md` end to end against the seeded environment, including the by-hand rule checks and the pair that makes the update clarification observable

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies
- **Phase 2 (Foundational)** — needs T001–T002; **blocks all stories**
- **Phase 3 (US1)** — needs Phase 2
- **Phase 4 (US2)** — needs US1's rule set and endpoints file
- **Phase 5 (US3)** — needs Phase 2 only; genuinely independent of US1 and US2
- **Phase 6 (Polish)** — needs the stories being shipped

### Story dependencies

- **US1 (P1)** — independent once Phase 2 is done. Delivers the MVP alone: time recorded, bad
  data refused, every rule proved both ways
- **US2 (P2)** — extends US1's rule set and endpoint file. Its field-scoped evaluation is
  meaningless without the rules existing
- **US3 (P3)** — reads only. Needs neither US1 nor US2, and is the first thing the cut order drops

### Within each story

- Tests before implementation. Against T006's stub they fail by **accepting** everything, which
  proves the test exercises a rule rather than a missing type
- The rule set before any handler; no handler may re-express a rule
- Handlers before endpoints; endpoints hold no logic

### Parallel opportunities

- **T003, T004, T005** — three records, three files, no dependencies between them
- **T008, T010, T011** — a DTO, a result mapper and two registration files
- **T012, T015, T016** — three independent groups of rule cases, though T013 and T014 add to the
  same file and follow T012
- **T025, T027** — a command and a handler in separate files
- **T030, T031, T032** — three independent test cases once T028 exists
- **T033, T035** — a query record and a read handler
- **T041, T042** — two documentation files

### Ordering trap worth naming

**T023 (concurrency) must come after T020, not with it.** Writing the transaction and the test
together invites shaping the test around what the implementation happens to do. The test states
what must be true — exactly one of two simultaneous submissions succeeds — and the transaction is
what makes it true.

---

## Implementation Strategy

### MVP first

1. Phase 1 → Phase 2 → Phase 3
2. **Stop and validate**: submit a conforming entry and one violation per rule
3. At that point the feature's whole argument is in place. Everything after it is correction and
   retrieval, both ordinary

### If the evening runs out

The plan fixes the cut order so it is not improvised here:

1. **Cut first**: Phase 5 entirely. The seed can be read through the rollup, and `docs/prd.md` §7
   names the plain endpoints as the cut candidate
2. **Cut second**: T035–T036's single-entry fetch; a caller can list with a filter
3. **Never cut**: T012–T018 and T023. The six rules, the twelve tests and the concurrency
   guarantee. P6 and §6.4 are why this feature exists

### Commit shape (P17)

Plan and tasks land separately from implementation. Within implementation, commit per phase. The
rule set and its tests belong in the same commit — a commit containing rules whose proof arrives
later is the shape §6.4 exists to prevent.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- Every new type, method, parameter and test method carries an XML doc comment (P25)
- **No rule may be expressed outside `TimeEntryRuleSet`.** If a handler or endpoint needs to know
  a limit, it asks the rule set; it does not restate the number
- Rule 6 has no refusing test because no submission can violate it. Its accepting test — record,
  change the rate, revise, assert unchanged — is the whole of its coverage, and it is the one that
  catches the mistake that would otherwise be silent
