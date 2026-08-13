# Implementation Plan: Weekly Billable Rollup

**Branch**: `003-weekly-billable-rollup` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-weekly-billable-rollup/spec.md`

## Summary

One stored procedure computes the whole report — weekly aggregation, per-client running
total, week-on-week change and within-week standing — and returns it already computed. The
application invokes it through an `Application`-declared interface implemented once in
`Infrastructure` with `SqlCommand`/`SqlDataReader`, wraps it in a handler, and exposes it as
a single authenticated endpoint. Correctness is pinned by tests that call the procedure
directly against a fixture whose expected values were computed by hand.

The two design decisions that carry the feature are both in `research.md`: weeks are
identified by a **day-count ordinal** rather than by week number (R1), which is what makes
"the preceding week" survive a year boundary; and the week-on-week change combines `LAG()`
with a **contiguity check** (R2), which is what lets sparse rows satisfy FR-008 without
materialising a row for every silent week.

## Technical Context

**Language/Version**: C# 13 on .NET 9, SDK pinned to 9.0.317 by `global.json`

**Primary Dependencies**: none added. `Microsoft.Data.SqlClient` already resolves through
`Microsoft.EntityFrameworkCore.SqlServer` and is already used directly by
`ProcedureApplier`. EF Core is present in the solution but is **not** on this feature's read
path (P5)

**Storage**: SQL Server 2022 in Docker; the report is a `CREATE OR ALTER` procedure in
`db/programmability/`, applied by the existing bootstrap step, never by a migration (P7)

**Testing**: xUnit against a real SQL Server 2022 container via Testcontainers (P11).
Procedure-level tests call the procedure directly; endpoint-level tests go through
`WebApplicationFactory`

**Target Platform**: cross-platform .NET 9 web service

**Project Type**: web service — minimal API over the existing four-project solution

**Performance Goals**: **none asserted, deliberately.** P8 forbids any speed claim this
feature could not back with a captured measurement, and the measurement is the next
feature's deliverable. This plan makes the report correct and leaves it un-indexed

**Constraints**: one database round trip per request; no new NuGet package; the procedure
file must contain no `GO` and nothing before the `CREATE OR ALTER` (R3); the day-count
ordinal must not depend on `SET DATEFIRST` or `SET LANGUAGE` (R1)

**Scale/Scope**: ~400,000 seeded entries across 24 months; at most 6,240 result rows
(60 clients × 104 weeks), fewer in practice

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result at the
bottom of this section.*

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ✅ | This is the artifact the repository exists for. Window functions, an execution path chosen deliberately over the ORM, and a fixture that can disprove the SQL |
| P2 | PRD out-of-scope binding | ✅ | Nothing from §2.2 enters. No second report, no caching, no pagination, no OData |
| P3 | One evening per spec | ⚠️ **at the cap** | ~17 files, ~30 tasks. Named as the tightest gate below rather than waved through |
| P4 | Four projects, dependencies inward | ✅ | `Api` → `Application` → `Domain`; `Infrastructure` → `Domain`. `Application` declares `IWeeklyBillableRollupReader`, `Infrastructure` implements it once. No new project |
| P5 | Right tool per access path | ✅ | The procedure is invoked with `SqlCommand`/`SqlDataReader` in the single `Infrastructure` type. No `FromSqlRaw`, no EF entity mapping. SC-009 is the criterion that makes a violation visible |
| P6 | Domain rules in domain and database | — | No writes in this feature. The storage constraints from 001 already govern the data it reads |
| P7 | Procedures source-controlled and idempotent | ✅ | One `CREATE OR ALTER` file under `db/programmability/`, applied by the existing verb. No migration touches it |
| P8 | Performance claims measured | ✅ | This feature asserts nothing about speed. The plan explicitly ships un-indexed so the next feature has a real "before" |
| P9 | Seed realistic in shape | ✅ | Delivered by 002. This feature consumes it and SC-007 checks one of its properties |
| P10 | The rollup is the headline | ✅ | `SUM() OVER`, `LAG()` and `DENSE_RANK()` all present and each doing real work. The frame and the contiguity check are commented, per R1–R2 |
| P11 | Real SQL Server | ✅ | Existing Testcontainers fixture, extended to apply procedures |
| P12 | Hand-computed fixture | ✅ | FR-021 to FR-023. The fixture is built by hand and its expectations written before the procedure runs; R7 explains how that is kept honest |
| P13 | Deliberate coverage | ✅ | Weighted to the procedure. The endpoint gets happy path, validation and auth |
| P14 | Spec before code | ✅ | Spec committed at `a645942`, refined at `d3e6af4` |
| P15 | Generated SQL reviewed line by line | ✅ | A dedicated review task, not a green test run. This feature is almost entirely the category P15 names |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec landed separately; plan and implementation follow |
| P18 | Quickstart is two commands | ✅ | The procedure is picked up by a bootstrap step that already exists and already handles this directory. Verifying that the quickstart still ends green is a task, not an assumption |
| P19 | Trade-offs stated | ✅ | The optional-parameter plan-reuse trade-off (R6) and the un-indexed ship are both stated rather than left to be noticed |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | `app.MapReportEndpoints()`; registrations go into the existing `AddLexTimeApplication()` and `AddLexTimeInfrastructure()` |
| P22 | Branch per spec | ✅ | On `003-weekly-billable-rollup` |
| P23 | Reproducible quality gate | ✅ | No new analyzer, no new package. `--warnaserror` stays clean |
| P24 | Security review before commit touching auth or SQL | ✅ | This feature is entirely SQL and touches the access boundary. Mandatory review task; R5 sets up the parameterisation that should make it uneventful |
| P25 | XML docs on everything | ✅ | Including test methods, as established in 001 and 002 |

**Gate result: PASS.** No MUST is violated, so the Complexity Tracking table stays empty.

**P3 is the one to watch**, and the honest position is that this spec sits at the cap rather
than comfortably inside it. It was already cut once — the covering index and the whole
before/after measurement were moved out before this plan was written. If it overruns anyway,
the cut order is fixed here so the decision is not made under time pressure later:

1. **Cut first**: endpoint-level tests reduce to one happy path plus one 401. The access
   boundary is already proven by 001's suite once it is retargeted.
2. **Cut second**: the single-client filter (FR-012) ships as a parameter the procedure
   honours but the endpoint does not expose, with the endpoint parameter following in the
   next feature.
3. **Never cut**: the procedure, the hand-computed fixture, and the year-boundary gap case.
   Those three *are* the feature. P10 and P12 both say so, and P3's own text says the spec is
   cut before the rule is.

**Post-Phase 1 re-check**: unchanged. The design added no project, no package and no
abstraction beyond the single interface P5 requires. The one thing Phase 1 changed is that
`SqlServerFixture` must now apply procedures as well as migrations (R8) — an extension of an
existing test helper, which touches no principle.

## Project Structure

### Documentation (this feature)

```text
specs/003-weekly-billable-rollup/
├── plan.md                              # This file
├── spec.md
├── research.md                          # Phase 0 — R1..R9
├── data-model.md                        # Phase 1
├── quickstart.md                        # Phase 1
├── checklists/requirements.md
├── contracts/
│   ├── usp-weekly-billable-rollup.md    # Procedure parameter and result-set contract
│   └── rollup-endpoint.md               # HTTP contract
└── tasks.md                             # /speckit-tasks output — not created here
```

### Source Code (repository root)

```text
db/programmability/
└── usp_WeeklyBillableRollup.sql              # NEW — the feature

src/
├── LexTime.Api/
│   ├── Endpoints/ReportEndpoints.cs          # NEW — MapReportEndpoints() (P21)
│   └── Program.cs                            # MOD — map reports, delete the ping placeholder
├── LexTime.Application/
│   ├── Reporting/
│   │   ├── IWeeklyBillableRollupReader.cs    # NEW — the interface Infrastructure implements
│   │   ├── WeeklyBillableRollupQuery.cs      # NEW — from, to, optional client
│   │   ├── WeeklyBillableRollupRow.cs        # NEW — one week for one client
│   │   ├── WeeklyBillableRollupResponse.cs   # NEW — envelope
│   │   └── GetWeeklyBillableRollupHandler.cs # NEW — the one use case (P4)
│   └── DependencyInjection.cs                # MOD — first real registration
└── LexTime.Infrastructure/
    ├── Reporting/SqlWeeklyBillableRollupReader.cs  # NEW — the only raw ADO.NET site (P5)
    └── DependencyInjection.cs                      # MOD — bind the interface

tests/LexTime.IntegrationTests/
├── RollupFixtureBuilder.cs             # NEW — the hand-computed dataset
├── WeeklyBillableRollupTests.cs        # NEW — procedure-level, the P12 tests
├── RollupEndpointTests.cs              # NEW — endpoint-level
├── SqlServerFixture.cs                 # MOD — apply procedures after migrating (R8)
├── DirectSql.cs                        # MOD — billable flag and rate as parameters (R9)
└── AuthBoundaryTests.cs                # MOD — retarget from /ping to the real endpoint
```

**Structure Decision**: no structural change. The feature adds one folder per layer
(`Reporting/`) inside three of the four existing projects and one file under the existing
`db/programmability/`. `LexTime.Application` gains its first real content — until now it held
only its registration method, which feature 001 documented as waiting for exactly this.

## Complexity Tracking

> No Constitution Check violations. Table intentionally empty.
