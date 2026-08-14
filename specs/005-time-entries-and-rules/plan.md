# Implementation Plan: Time Entries and the Domain Rules

**Branch**: `005-time-entries-and-rules` | **Date**: 2026-08-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-time-entries-and-rules/spec.md`

## Summary

The six rules become one pure evaluation in `LexTime.Domain`, over an explicit set of facts the
caller supplies. That is what makes them enforceable in one place (FR-011) while keeping
`LexTime.Domain` free of persistence — four of the six need data the domain cannot fetch for
itself, so the domain states what it needs and is told, rather than reaching.

Five handlers in `LexTime.Application` gather those facts, run the rules, and either persist or
return violations. The endpoints validate shape, invoke a handler, and map a violation to a
problem response. Nothing about a rule appears in an endpoint.

Two decisions carry the feature. **Rule 4 needs a clock**, so the current date is injected rather
than read — without that, every test of the 90-day window rots on a date (R3). And **rule 3 is a
read-then-write**, so it is defeatable by two concurrent submissions unless the read and the
write share a transaction that will not let them interleave (R4).

## Technical Context

**Language/Version**: C# 13 on .NET 9, SDK pinned to 9.0.317

**Primary Dependencies**: none added. The clock is `System.TimeProvider`, which is in the base
class library; its test double is hand-written rather than taken from a package (R3)

**Storage**: SQL Server 2022. EF Core owns this write path (P5) — no raw ADO.NET, which belongs
only to the reporting read

**Testing**: xUnit against a real SQL Server 2022 container (P11), plus pure rule tests that need
no container at all and run in milliseconds (R10)

**Target Platform**: cross-platform .NET 9 web service

**Project Type**: web service — five endpoints on the existing minimal API

**Performance Goals**: none stated and none measured. This feature makes no speed claim, so P8
has nothing to bind

**Constraints**: rules enforced in exactly one place (FR-011); storage constraints must survive
(FR-012, SC-010); rule-4 tests must not depend on the calendar (FR-026, SC-009); a refused write
must leave data untouched (FR-015)

**Scale/Scope**: 5 endpoints, 6 rules, ~18 files. Writes are single-row; the listing pages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result at the
bottom of this section.*

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ✅ | Six real rules, enforced once, tested both ways. The part of a CRUD surface a reviewer actually reads |
| P2 | PRD out-of-scope binding | ✅ | Nothing from §2.2 enters. No approval workflow, no soft delete, no ownership model, no rate history |
| P3 | One evening per spec | ⚠️ **at the cap** | ~18 files. The rules are the work; the listing is padding. Cut order fixed below |
| P4 | Four projects, dependencies inward | ⚠️ **reviewed, passes** | See the note below on the port. Rules in `Domain`, five handlers in `Application`, endpoints hold no logic |
| P5 | Right tool per access path | ✅ | EF Core owns these writes and simple reads, which is exactly what P5 assigns it. The reporting path is untouched |
| P6 | **Domain rules in the domain, and in the database** | ✅ | The whole feature. All six in `Domain`; the duration constraints stay in the schema and SC-010 re-proves they still bite |
| P7 | Procedures source-controlled | — | No procedure change |
| P8 | Performance claims measured | ✅ | No claim made. Feature 004's measurement is not touched or re-quoted |
| P9 | Seed realistic in shape | ✅ | Consumed unchanged as the fixture for rule 5 and the listing |
| P10 | The rollup is the headline | ✅ | Not modified. FR notes that entries written here must be visible to it |
| P11 | Real SQL Server | ✅ | For everything that touches storage. The pure rule tests need no database, which is why they can be exhaustive |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged |
| P13 | Deliberate coverage | ✅ | Weighted to the rules: twelve tests minimum before any endpoint test exists |
| P14 | Spec before code | ✅ | Spec committed at `618e169` |
| P15 | Generated SQL reviewed line by line | ✅ | Little SQL here, but the transaction and locking in R4 is exactly the category and gets a review task |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec landed separately; plan and implementation follow |
| P18 | Quickstart is two commands | ✅ | Unchanged. This feature adds endpoints, not steps |
| P19 | Trade-offs stated | ✅ | The port (below), the isolation level (R4) and the field-scoped update rules are all named rather than buried |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | `app.MapTimeEntryEndpoints()`; registrations into the existing per-layer methods |
| P22 | Branch per spec | ✅ | On `005-time-entries-and-rules` |
| P23 | Reproducible quality gate | ✅ | No new analyzer, no new package — including for the clock's test double |
| P24 | Security review before commit touching SQL | ✅ | Review task. The write path is EF-parameterised; the one hand-written query is the daily total |
| P25 | XML docs on everything | ✅ | Including test methods |

**Gate result: PASS.** Complexity Tracking stays empty — but two entries below are judgements
rather than clean passes, and both are recorded here so they are visible rather than assumed.

**P4 and the persistence port.** `LexTime.Application` cannot reference `LexTime.Infrastructure`,
so a write use case reaches storage through an interface it declares. P4 bans "a generic
repository over `DbSet<T>`" on the grounds that it "adds a layer that only forwards calls". The
port here — `ITimeEntryStore` — is **not generic**, serves one aggregate, exists in one copy, and
its most important method (`SumMinutesForUserOnDateAsync`) has no `DbSet` equivalent and is a
domain question rather than a CRUD operation. Its remaining methods do forward, and that is the
honest cost of the layering P4 itself mandates: an interface with a single implementation is
what P4's own text says to expect here. Recorded in R2 with the alternatives that were rejected.

**P3 cut order**, fixed now rather than improvised later:

1. **Cut first**: the listing filters and paging (US3, `ListTimeEntriesHandler` and its tests).
   The seed can be read through the rollup, and §7 names the plain endpoints as the cut candidate.
2. **Cut second**: `GET /time-entries/{id}` as a separate use case; a caller can list with a
   filter.
3. **Never cut**: the six rules, the twelve tests, the transaction in R4, and the storage-constraint
   re-proof. P6 and §6.4 are the reason this feature exists.

**Post-Phase 1 re-check**: unchanged. The design adds no project and no package. The one thing
Phase 1 altered is that the update command compares against stored values rather than carrying
"changed" flags (R5), which removed a class of API ambiguity rather than adding one.

## Project Structure

### Documentation (this feature)

```text
specs/005-time-entries-and-rules/
├── plan.md                      # This file
├── spec.md
├── research.md                  # Phase 0 — R1..R10
├── data-model.md                # Phase 1
├── quickstart.md                # Phase 1
├── checklists/requirements.md
├── contracts/
│   ├── time-entry-endpoints.md  # HTTP contract for the five routes
│   └── domain-rules.md          # The six rules as a testable contract
└── tasks.md                     # /speckit-tasks output — not created here
```

### Source Code (repository root)

```text
src/
├── LexTime.Domain/Rules/
│   ├── DomainRule.cs                     # NEW — the six, named and numbered
│   ├── TimeEntryFacts.cs                 # NEW — what the rules must be told
│   ├── RuleViolation.cs                  # NEW — which rule, which value, why
│   └── TimeEntryRuleSet.cs               # NEW — the one evaluation (FR-011)
├── LexTime.Application/TimeEntries/
│   ├── ITimeEntryStore.cs                # NEW — the port (see P4 note)
│   ├── TimeEntryDto.cs                   # NEW — with its ToDto() extension (P4)
│   ├── RecordTimeEntryCommand.cs         # NEW
│   ├── ReviseTimeEntryCommand.cs         # NEW
│   ├── ListTimeEntriesQuery.cs           # NEW
│   ├── RecordTimeEntryHandler.cs         # NEW — one handler per use case (P4)
│   ├── ReviseTimeEntryHandler.cs         # NEW
│   ├── DeleteTimeEntryHandler.cs         # NEW
│   ├── GetTimeEntryHandler.cs            # NEW
│   ├── ListTimeEntriesHandler.cs         # NEW
│   └── DependencyInjection.cs            # MOD — register the five
├── LexTime.Infrastructure/TimeEntries/
│   ├── EfTimeEntryStore.cs               # NEW — EF Core; the transaction from R4
│   └── DependencyInjection.cs            # MOD — bind the port
└── LexTime.Api/
    ├── Endpoints/TimeEntryEndpoints.cs   # NEW — MapTimeEntryEndpoints() (P21)
    ├── Endpoints/RuleViolationResults.cs # NEW — violation → problem response (R6)
    └── Program.cs                        # MOD — map them

tests/LexTime.IntegrationTests/
├── TimeEntryRuleTests.cs                 # NEW — the twelve, pure and fast
├── TimeEntryWriteTests.cs                # NEW — create, revise, delete over HTTP
├── TimeEntryListingTests.cs              # NEW — filters and paging
└── FixedClock.cs                         # NEW — five-line TimeProvider double (R3)
```

**Structure Decision**: no structural change. One folder per layer, mirroring how `Reporting/`
landed in feature 003. `LexTime.Domain` gains its first behaviour — it has held only property
bags since feature 001, and P6 is the principle that finally gives it something to do.

## Complexity Tracking

> No Constitution Check violations. The two judgement calls are recorded in the Constitution
> Check above and in `research.md` rather than here, because neither is a departure from a
> principle — they are readings of one.
