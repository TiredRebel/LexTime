# Implementation Plan: Clients, Matters and Timekeepers

**Branch**: `006-clients-and-matters` | **Date**: 2026-08-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-clients-and-matters/spec.md`

## Summary

Ten endpoints, ten handlers, three ports. Almost all of it is mechanical — `docs/prd.md` §7 says
so plainly, and calls these endpoints "generated in bulk but reviewed in bulk too". Two parts are
not.

**Uniqueness is answered by the database, not guessed at before it.** A collision is caught from
the constraint that already exists and translated by index name into an actionable response
(R2). No pre-check: a check-then-insert is a race, and it does not remove the need for the catch
it was meant to avoid.

**Deactivation is verified from the other side.** A matter closed here has to be refused by
feature 005's write path and its existing entries have to keep appearing in feature 003's rollup.
Tests in this feature assert both, because a boundary nothing checks from across the line is not
a boundary (R6).

## Technical Context

**Language/Version**: C# 13 on .NET 9, SDK pinned to 9.0.317

**Primary Dependencies**: none added

**Storage**: SQL Server 2022. **No schema change and no migration** — the two uniqueness
constraints this feature surfaces have existed since feature 001

**Testing**: xUnit against a real SQL Server 2022 container (P11)

**Target Platform**: cross-platform .NET 9 web service

**Project Type**: web service — ten endpoints on the existing minimal API

**Performance Goals**: none stated, none measured. This feature makes no speed claim

**Constraints**: no change to time entries, the six rules, the rollup or its index; timekeepers
stay read-only (SC-009); codes and matter numbers immutable after creation (FR-011); a refused
write leaves data untouched (FR-016)

**Scale/Scope**: 10 endpoints, ~25 files. Writes are single-row; three listings page

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result at the
bottom of this section.*

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ⚠️ **honestly, weak** | This is the CRUD `docs/prd.md` §4 admits is sixteen ordinary endpoints. It moves the needle by completing the surface, not by being interesting. Named rather than dressed up |
| P2 | PRD out-of-scope binding | ✅ | Nothing from §2.2 enters. No delete, no rate cards, no RBAC, no merge |
| P3 | One evening per spec | ⚠️ **at the cap** | Ten handlers. Mechanical, but volume alone reaches the limit. Cut order fixed below |
| P4 | Four projects, dependencies inward | ⚠️ **reviewed, passes** | Three more ports. The cost is now visible and is named below rather than absorbed silently |
| P5 | Right tool per access path | ✅ | EF Core owns all of it — writes and simple entity reads, exactly what P5 assigns it |
| P6 | Domain rules in domain and database | ✅ | The uniqueness rules stay in the schema; this feature makes them answerable. SC-011 re-proves the constraint still bites, as feature 005's SC-010 did for durations |
| P7 | Procedures source-controlled | — | No procedure change |
| P8 | Performance claims measured | ✅ | No claim made. Feature 004's numbers are not touched or re-quoted |
| P9 | Seed realistic in shape | ✅ | Consumed unchanged, and the reused matter numbers across clients are exactly what FR-007 must permit |
| P10 | The rollup is the headline | ✅ | Not modified. FR-014 requires its output to survive a deactivation, and a test asserts it |
| P11 | Real SQL Server | ✅ | Everything here touches storage; there is no pure tier to add |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged |
| P13 | **Deliberate coverage** | ✅ | The principle that governs this feature: "trivial CRUD gets one happy-path and one 404 test each". The exceptions are the collisions and the deactivation boundary, which get more |
| P14 | Spec before code | ✅ | Spec committed at `484a32d` |
| P15 | Generated SQL reviewed line by line | ✅ | Little SQL, but the exception-to-conflict translation is the delicate part and gets a review task |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec landed separately; plan and implementation follow |
| P18 | Quickstart is two commands | ✅ | Unchanged |
| P19 | Trade-offs stated | ✅ | The port count, the absent pre-check, and this feature's weak P1 standing are all named |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | `MapClientEndpoints()`, `MapMatterEndpoints()`, `MapTimekeeperEndpoints()` |
| P22 | Branch per spec | ✅ | On `006-clients-and-matters` |
| P23 | Reproducible quality gate | ✅ | No new analyzer, no new package |
| P24 | Security review before commit touching SQL | ✅ | Review task. The write path is EF-parameterised throughout |
| P25 | XML docs on everything | ✅ | Including test methods |

**Gate result: PASS.** Complexity Tracking stays empty. Three entries above are judgements rather
than clean ticks and are expanded here so they are visible rather than assumed.

**P1, honestly.** This feature does not make a reviewer more confident in ten minutes; the rollup
and the measurement do that. It exists because §4 promises these endpoints and a half-built API
is worse signal than a complete plain one. Saying so is more useful than claiming otherwise —
and it is why P13, not P1, drives the test weighting here.

**P4 and the port count.** This adds `IClientStore`, `IMatterStore` and `ITimekeeperStore` to the
`ITimeEntryStore` feature 005 introduced. **Four forwarding ports is now a visible cost, not a
one-off.** The reasoning recorded in feature 005's R2 still holds — each is non-generic, serves
one aggregate, and exists because `Application` cannot see `Infrastructure` — but the honest
observation is that P4's layering charges this on every aggregate, and the charge compounds.
It is recorded rather than re-argued; changing it is a constitution amendment, not a feature
decision.

**P3 cut order**, fixed now rather than improvised later:

1. **Cut first**: the two timekeeper endpoints. They are pure reads over seeded data with no
   write path at all, and nothing else depends on them.
2. **Cut second**: `GET /clients/{id}` and `GET /matters/{id}` as separate use cases; both are
   reachable by listing with a filter.
3. **Never cut**: the two collision paths, the deactivation boundary tests, and the
   constraint re-proof. Those are the only parts of this feature that are not boilerplate.

**Post-Phase 1 re-check**: unchanged. No project, package or abstraction beyond the three ports
the layering requires.

## Project Structure

### Documentation (this feature)

```text
specs/006-clients-and-matters/
├── plan.md                     # This file
├── spec.md
├── research.md                 # Phase 0 — R1..R9
├── data-model.md               # Phase 1
├── quickstart.md               # Phase 1
├── checklists/requirements.md
├── contracts/
│   ├── client-endpoints.md
│   └── matter-and-timekeeper-endpoints.md
└── tasks.md                    # /speckit-tasks output — not created here
```

### Source Code (repository root)

```text
src/
├── LexTime.Application/Parties/
│   ├── IClientStore.cs · IMatterStore.cs · ITimekeeperStore.cs      # NEW — three ports
│   ├── PartyDtos.cs                                                 # NEW — three DTOs + ToDto()
│   ├── PartyCommands.cs                                             # NEW — commands and queries
│   ├── PartyWriteResult.cs                                          # NEW — success, conflict, missing
│   ├── ClientHandlers.cs                                            # NEW — register, revise, get, list
│   ├── MatterHandlers.cs                                            # NEW — open, revise, get, list
│   ├── TimekeeperHandlers.cs                                        # NEW — get, list
│   └── DependencyInjection.cs                                       # MOD
├── LexTime.Infrastructure/Parties/
│   ├── EfClientStore.cs · EfMatterStore.cs · EfTimekeeperStore.cs   # NEW
│   ├── UniqueConstraintTranslator.cs                                # NEW — the delicate part (R2)
│   └── DependencyInjection.cs                                       # MOD
└── LexTime.Api/
    ├── Endpoints/ClientEndpoints.cs · MatterEndpoints.cs
    │              TimekeeperEndpoints.cs                            # NEW (P21)
    └── Program.cs                                                   # MOD

tests/LexTime.IntegrationTests/
├── ClientEndpointTests.cs           # NEW — happy path, 404, both collision cases
├── MatterEndpointTests.cs           # NEW — same, plus the composite-uniqueness case
├── TimekeeperEndpointTests.cs       # NEW — read-only, and provably unwritable (SC-009)
└── DeactivationBoundaryTests.cs     # NEW — the cross-feature assertions (R6)
```

**Structure Decision**: one `Parties/` folder per layer rather than three folders of two files.
Clients, matters and timekeepers are read and written together, share one result type and one
conflict translator, and splitting them would spread eight small files across six directories.
`TimeEntries/` stayed separate because it carries the rules; this does not.

## Complexity Tracking

> No Constitution Check violations. The three judgement calls are expanded in the Constitution
> Check above and in `research.md`, because none is a departure from a principle — two are
> readings of one, and the third is an honest assessment against it.
