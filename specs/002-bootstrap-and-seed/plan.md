# Implementation Plan: Bootstrap and Seed

**Branch**: `002-bootstrap-and-seed` | **Date**: 2026-08-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-bootstrap-and-seed/spec.md`

## Summary

Turn the three manual commands feature 001 left behind into the two-command quickstart
`docs/prd.md` §6.3 requires: one PowerShell script that verifies prerequisites, brings up
the container, waits for readiness, applies migrations, applies stored procedures,
generates ~400,000 deterministic time entries, verifies their distribution, and prints a
development token.

Phase 0 decisions were carried forward from the original combined planning run. Validating
them against the code feature 001 actually shipped changed one of them materially — see
R0 in [research.md](./research.md): **migrations are applied by the application, not by
`dotnet ef`**, which removes a tool installation from the quickstart and is the difference
between P18 being satisfied and being approximately satisfied.

## Technical Context

**Language/Version**: C# 13 on .NET 9 (SDK pinned to 9.0.317) and PowerShell 7 for the
bootstrap script

**Primary Dependencies**: existing only — EF Core 9 for migrations,
`Microsoft.Data.SqlClient` (already transitive) for `SqlBulkCopy` and for applying
procedure files. **No new package is added by this feature.**

**Storage**: SQL Server 2022 in the container defined by `docker-compose.yml`

**Testing**: xUnit with the existing Testcontainers fixture, seeding at reduced volume

**Target Platform**: Cross-platform; `pwsh` on Windows, macOS and Linux

**Performance Goals**: seeding under 60 s (FR-022); full bootstrap under 3 min with the
image present (SC-002)

**Constraints**: no fifth project (P4); procedures never applied by a migration (P7);
generation reads no clock and no machine entropy (FR-020, FR-021); reset drops the database
only (FR-006)

**Scale/Scope**: 25 users, 60 clients, ~220 matters, ~400,000 time entries across 24
months; one script; one seeding path

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` v2.0.0.

### P3 — One evening per spec

**PASS, with a named cut line.** The work is: a data generator, a bulk-load path, a
verification pass, a token minting path, a command-line surface on the existing host, and
a PowerShell script that orchestrates them. The generator is the only part with real
thinking in it; the rest is plumbing over infrastructure that already exists.

If it overruns, the cut is **token minting** (FR-024, FR-025). A reviewer can still see
the boundary work — feature 001 proves it accepts and rejects — and the token can move to
the feature that adds the first real protected endpoint. Seeding and the script are not
cuttable: they are what makes the quickstart two commands.

### P4 — Four projects, no mediator or mapping library

**PASS.** No new project. The generator and the bulk-load path are classes in
`LexTime.Infrastructure`; the command-line surface is a branch in `LexTime.Api`'s existing
entry point. The obvious design — a `LexTime.SeedTool` console app — would be a fifth
project and a violation, and is rejected on the MUST.

`LexTime.Application` gains nothing here. It stays as it is until feature 003.

### P5 — Right tool per access path

**PASS, and worth stating precisely.** `SqlBulkCopy` is neither ORM writes nor reporting
reads; it is bulk load, which the constitution does not name. It belongs in
`Infrastructure` beside the `DbContext`, and it does not go through EF change tracking —
inserting 400,000 tracked entities would take minutes and defeat FR-022.

This is the first place the seed bypasses application validation entirely, which is
exactly why feature 001 put the duration rules in the schema (P6).

### P7 — Procedures source-controlled, never created by a migration

**PASS.** The script enumerates `db/programmability/*.sql` in sorted order and executes
each file's contents. `db/programmability/` currently holds only `.gitkeep`; an empty
directory reports "no procedures to apply" and continues (FR-010).

### P8 — Measured, not asserted

**PASS, and this feature is what makes feature 003's measurement possible.** FR-020 and
FR-021 exist so the before/after index comparison has a stable dataset underneath it. A
generator that read the clock would silently invalidate every number `docs/performance.md`
publishes. The reference date is a committed constant and must be cited alongside any
measurement.

### P11 / P13 — Real SQL Server, deliberate coverage

**PASS, with a design decision that makes it affordable.** The original plan said seed
distribution would be verified by the script rather than by tests, because reseeding
400,000 rows per test run costs minutes. That reasoning holds for the full volume — but it
does not hold if the volume is a parameter.

The generator takes its volumes as inputs. Tests seed at 1/100 scale into the existing
Testcontainers fixture and assert the same invariants: weekday concentration, client skew,
non-billable share, zero duration violations, inactive-with-history, and **determinism**
(two generations from the same inputs produce identical rows). That last one is the
property most worth a test and the least likely to be caught by eye.

The script's own verification pass still runs at full volume against the real dataset,
because that is where a developer needs the answer.

### P18 — The quickstart is two commands and works from cold

**PASS — and this is the feature that makes it true.** See R0: applying migrations through
the application rather than `dotnet ef` removes the `dotnet tool install` step that
feature 001's README currently documents. After this feature the prerequisites are Docker
and the SDK, exactly as the criterion states.

### P24 — Security review on auth and SQL

**PASS, with two review items identified up front:**

1. **Procedure application** constructs a `SqlCommand` from file contents, so
   `CommandText` is not a literal. `.editorconfig` sets **CA2100 to `error`**, not warning
   — so this will fail the build until suppressed at that call site with a justification
   naming the input as source-controlled.
2. **Token minting** puts signing-key handling in a second place. The mitigation is R5:
   mint inside the API process, reusing `AuthenticationSetup`'s constants, so exactly one
   place knows the key and the claim shape.

### Remaining principles

| Principle | Verdict | Note |
|---|---|---|
| P1 hiring signal | PASS | The cold start is the first thing a reviewer runs |
| P2 PRD out-of-scope binding | PASS | Nothing from §2.2 introduced |
| P6 rules in domain and database | PASS | The schema half already exists and is what protects the bulk path |
| P9 realistic seed shape | PASS | FR-013 to FR-017, bands in SC-004 and SC-007 |
| P10 rollup is the headline | N/A | Feature 003 |
| P12 hand-computed fixture | N/A | Feature 003 |
| P14 spec before code | PASS | Spec, clarifications and this plan precede implementation |
| P15 generated SQL reviewed | PASS | Applies to the bulk-insert column mapping and the verification queries |
| P16 agent mistakes logged | PASS | `docs/agent-log.md` exists and is appended to |
| P17 separate commits | PASS | |
| P19 trade-offs stated | PASS | README gains the reset and teardown commands |
| P20 English | PASS | |
| P21 composition roots are extension methods | PASS | Seeding registers through `AddLexTimeInfrastructure()` |
| P22 branch per spec | **ACTION REQUIRED** | The branch does not exist yet — see below |
| P23 reproducible quality gate | PASS | Inherited from `Directory.Build.props`; no project opts out |
| P25 XML docs on everything | PASS | Plus comment-based help on the PowerShell script, which is its equivalent |

**Gate result: pass.** No MUST is violated.

### P22 — branch not yet created

Work must not start on `001-solution-and-schema`. Two orderings are available, and the
choice is the author's:

- **Merge 001 into `main` first, then branch 002 from `main`.** Cleaner history; each
  feature reaches `main` as one merge, exactly as P22 describes.
- **Branch 002 from `001-solution-and-schema`.** Stacked, because 002 genuinely cannot run
  without 001's schema. Honest, but 002's branch then contains 001's commits.

The first is preferable and is what P22 has in mind.

## Project Structure

### Documentation (this feature)

```text
specs/002-bootstrap-and-seed/
├── plan.md              # This file
├── research.md          # Phase 0 — carried forward and revalidated
├── data-model.md        # Phase 1 — generation shape, not schema
├── quickstart.md        # Phase 1
├── contracts/
│   ├── bootstrap-cli.md # Script parameters, output, exit codes (carried forward)
│   └── host-cli.md      # The application's command-line surface
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Existing files this feature changes are marked; everything else is new.

```text
README.md                                   # CHANGED: quickstart becomes two commands
docker-compose.yml                          # unchanged
db/programmability/                         # unchanged; still empty until feature 003

scripts/
└── Initialize-LocalDb.ps1                  # NEW: the whole orchestration

src/LexTime.Api/
└── Program.cs                              # CHANGED: command-line branch before RunAsync

src/LexTime.Infrastructure/
├── DependencyInjection.cs                  # CHANGED: register seeding services
├── Maintenance/
│   ├── MigrationRunner.cs                  # NEW: applies migrations in-process (R0)
│   ├── ProcedureApplier.cs                 # NEW: applies db/programmability/*.sql (P7)
│   └── DevelopmentTokenMinter.cs           # NEW: mints the printed token (R5)
└── Seeding/
    ├── SeedOptions.cs                      # NEW: volumes, reference date, generator seed
    ├── SeedDataGenerator.cs                # NEW: deterministic generation, no I/O
    ├── BulkSeeder.cs                       # NEW: SqlBulkCopy load
    └── SeedVerifier.cs                     # NEW: distribution checks (FR-023)

tests/LexTime.IntegrationTests/
├── SeedGeneratorTests.cs                   # NEW: determinism and shape, pure, no database
└── SeedVerificationTests.cs                # NEW: reduced-volume seed against the container
```

**Structure Decision**: Unchanged from feature 001 — four projects plus tests, per P4 and
`docs/prd.md` §5. Generation splits into a pure generator and a separate loader so the
generator's determinism and distribution can be tested without a database at all, and the
loader can be tested at reduced volume against the real one.

## Complexity Tracking

> Filled only where the Constitution Check found a departure requiring justification.

No departures. The one open item (P22, branch creation) is a sequencing action, not a
principle violation to be waived.
