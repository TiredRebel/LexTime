# Implementation Plan: Index and Measured Performance

**Branch**: `004-index-and-measurement` | **Date**: 2026-08-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-index-and-measurement/spec.md`

## Summary

The covering index joins the schema through an EF migration, so a fresh clone gets it. A new
`measure` verb on the existing host CLI does everything else: it drops the index, runs the
rollup, restores it, runs again, and captures `SET STATISTICS IO`/`TIME` output and the actual
execution plan for each of four combinations. Nothing new is installed — the verb reuses the
connection the quickstart already configures, which is what makes the measurement reproducible
rather than merely reported (FR-013, FR-015).

Two findings from Phase 0 shape the design. **A dropped index is not restored by re-running
migrations** (R7) — EF records the migration as applied and will not notice the object is gone,
so an interrupted measurement leaves a database that looks correct and is not; the verb both
self-heals on entry and restores in a `finally`. And **result-set equivalence is proved at both
scales** (R8): a row-by-row test at a hundredth scale for precision, and a full-scale checksum
inside the measurement itself, because SC-001's claim is about all 24 months and no test may
take three minutes.

## Technical Context

**Language/Version**: C# 13 on .NET 9, SDK pinned to 9.0.317

**Primary Dependencies**: none added. `Microsoft.Data.SqlClient` and EF Core 9 are already
present; the measurement uses the former directly

**Storage**: SQL Server 2022 in Docker. The index is a table structure and therefore belongs
to a migration — constitution P7 keeps only *procedures* out of migrations

**Testing**: xUnit against a real SQL Server 2022 container via Testcontainers (P11). The
equivalence test runs at 1/100 scale; the full-scale claim is discharged by the measurement run

**Target Platform**: cross-platform .NET 9

**Project Type**: web service with a maintenance CLI, both hosted by `LexTime.Api`

**Performance Goals**: **none. This feature has no target and states none.** Its output *is* a
measurement. Writing an expected figure into this plan would be the failure P8 describes, and
would give the implementation a number to aim at instead of one to take

**Constraints**: no tool beyond what the quickstart already requires (FR-015); the schema must
end indexed however the run terminates (FR-014); the committed seed's volumes and reference
date are fixed and may not be adjusted to improve a result (FR-018)

**Scale/Scope**: 400,000 entries over 2024-08-13 to 2026-08-13; four measured combinations;
five readings each

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result at the
bottom of this section.*

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ✅ | Plans, read counts and an honest account of what changed. The section a senior reviewer turns to first |
| P2 | PRD out-of-scope binding | ✅ | Nothing from §2.2 enters. No load harness, no second index, no query rewrite |
| P3 | One evening per spec | ⚠️ **at the cap** | ~14 files. The index is the cheap part; the verb and the capture are the work. Cut order fixed below |
| P4 | Four projects, dependencies inward | ✅ | Measurement logic in `Infrastructure`, invoked by the existing verb dispatcher in `Api/Maintenance`. That file is one of the three the amended P4 enumerates as permitted to name `Infrastructure` types |
| P5 | Right tool per access path | ✅ | The measurement is a reporting concern and uses `SqlCommand` directly. No EF on this path |
| P6 | Domain rules in domain and database | — | No writes and no rule changes |
| P7 | Procedures source-controlled, not in migrations | ✅ | The procedure is untouched (FR: no query rewrite). The *index* is a table structure and correctly belongs to a migration |
| P8 | **Performance claims measured, never asserted** | ✅ | This feature is P8. Every figure comes from a captured run; the plan states no expected value; FR-018 forbids improving a result by changing the dataset |
| P9 | Seed realistic in shape | ✅ | Consumed unchanged. FR-018 makes altering it a defect rather than a mitigation |
| P10 | The rollup is the headline | ✅ | This is the documentation half of that claim |
| P11 | Real SQL Server | ✅ | Testcontainers for the equivalence test; the measurement runs against the real local container |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged. FR-004 makes editing any expected value a defect |
| P13 | Deliberate coverage | ✅ | One test class: index presence and result-set equivalence |
| P14 | Spec before code | ✅ | Spec committed at `850c643` |
| P15 | Generated SQL reviewed line by line | ✅ | The index DDL and the measurement SQL are both review tasks |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec landed separately; plan and implementation follow |
| P18 | Quickstart is two commands | ✅ | Unchanged. The `measure` verb is an extra capability, not an extra step — and it is documented |
| P19 | Trade-offs stated | ✅ | The cache convention (R5), the server-wide effect of clearing buffers (R5), and read-counts-over-elapsed-time (R6) are all stated rather than buried |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | One new registration in the existing `AddLexTimeInfrastructure()` |
| P22 | Branch per spec | ✅ | On `004-index-and-measurement`, off the merged `main` |
| P23 | Reproducible quality gate | ✅ | No new analyzer, no new package |
| P24 | Security review before commit touching SQL | ✅ | The verb executes DDL. Review task; R4 sets up the constants that should make it uneventful |
| P25 | XML docs on everything | ✅ | Including test methods |

**Gate result: PASS.** No MUST is violated; Complexity Tracking stays empty.

**P3 is again the tight one.** If it overruns, the cut order is fixed here rather than
improvised later:

1. **Cut first**: the single-client combination (US3). It halves the measured combinations from
   four to two and is explicitly the P3 story.
2. **Cut second**: the readings drop from five to three. The spread gets wider error bars and
   the read counts are unaffected, being deterministic.
3. **Never cut**: the index, the equivalence proof, the captured plans, and the honest account.
   FR-003 is the one that protects correctness and FR-020 is the one that protects credibility.

**Post-Phase 1 re-check**: unchanged. The design adds no project, package or abstraction. R7's
self-heal touches the existing `state` verb, which is a report-only change.

## Project Structure

### Documentation (this feature)

```text
specs/004-index-and-measurement/
├── plan.md                          # This file
├── spec.md
├── research.md                      # Phase 0 — R1..R10
├── data-model.md                    # Phase 1
├── quickstart.md                    # Phase 1
├── checklists/requirements.md
├── contracts/
│   ├── measure-verb.md              # CLI contract for the measurement
│   └── performance-document.md      # What the published account must contain
└── tasks.md                         # /speckit-tasks output — not created here
```

### Source Code (repository root)

```text
src/
├── LexTime.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/TimeEntryConfiguration.cs   # MOD — declare the index; replace the
│   │   │                                              #       comment explaining its absence
│   │   └── Migrations/*_AddWorkDateBillableIndex.cs   # NEW — generated
│   ├── Measurement/
│   │   ├── CoveringIndex.cs                           # NEW — its name, DDL, presence check
│   │   ├── RollupMeasurer.cs                          # NEW — runs one combination, captures
│   │   └── MeasurementReading.cs                      # NEW — one captured run
│   ├── Maintenance/DatabaseStateInspector.cs          # MOD — report index presence (R7)
│   └── DependencyInjection.cs                         # MOD — register the measurer
└── LexTime.Api/Maintenance/
    ├── MaintenanceCommands.cs                         # MOD — the `measure` verb
    └── ExitCodes.cs                                   # MOD — if a new failure class is needed

docs/
├── performance.md                        # NEW — the published account
└── performance/                          # NEW — captured artefacts
    ├── *.sqlplan                         #       four plans, openable
    └── statistics-*.txt                  #       raw STATISTICS IO/TIME output, verbatim

tests/LexTime.IntegrationTests/
└── CoveringIndexTests.cs                 # NEW — index present; results identical either way

README.md                                 # MOD — four placeholders replaced
scripts/Initialize-LocalDb.ps1            # MOD — mention `measure` in its closing hint
```

**Structure Decision**: no structural change. One new folder in `Infrastructure`, one verb on
an existing dispatcher, one new documentation folder. The raw `STATISTICS` output is committed
verbatim beside the summary table so that the numbers in the table can be checked against their
source rather than taken on trust — the same reason the plans are committed as files.

## Complexity Tracking

> No Constitution Check violations. Table intentionally empty.
