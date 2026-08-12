# Implementation Plan: Solution and Schema

**Branch**: `001-solution-and-schema` | **Date**: 2026-08-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-solution-and-schema/spec.md`

## Summary

Create the four-project solution with its dependency direction enforced by project
references, the build and documentation gate that every later feature inherits, the four
`dbo` tables with their storage-level constraints and initial migration, the container
definition for the database, token validation, and a health endpoint that reports each
check individually.

The stack is fixed by `docs/prd.md` §2.1 and constitution P4, P5 and P7. The decisions
this plan makes are how the documentation gate applies to a test project, how constraints
are declared so the model and schema cannot drift, and what the health check must actually
execute to be worth having.

**Scope note**: this feature was split out of the original `001-local-environment-schema`
after that spec's Constitution Check failed P3. The bootstrap script and data seeding are
[feature 002](../002-bootstrap-and-seed/spec.md).

## Technical Context

**Language/Version**: C# 13 on .NET 9, SDK pinned to 9.0.317 by `global.json`

**Primary Dependencies**: EF Core 9 (SQL Server provider) for the model and migrations;
ASP.NET Core minimal API with JWT bearer validation; ASP.NET Core health checks. No
mediator library and no mapping library (P4).

**Storage**: SQL Server 2022 in Docker Compose, schema `dbo`

**Testing**: xUnit with Testcontainers against a real SQL Server 2022 image (P11)

**Target Platform**: Cross-platform; Linux container image, Windows or Linux host

**Project Type**: Web service, four projects plus one test project (P4)

**Performance Goals**: health check state change visible within 5 s (SC-004). No other
performance claim is made by this feature

**Constraints**: `Application` carries no third-party runtime dependency (P4); no
covering index (FR-014); no constraint on the billing date (FR-012); no project may opt
out of the documentation gate (FR-005)

**Scale/Scope**: 4 tables, 1 unauthenticated endpoint, 1 temporary placeholder protected
route, 5 projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` v2.0.0.

### P4 — Four projects, dependencies inward, no mediator or mapping library

**PASS.** `LexTime.Api`, `LexTime.Application`, `LexTime.Domain`,
`LexTime.Infrastructure`, plus `LexTime.IntegrationTests`. Project references enforce the
direction, so a violation is a compile error rather than a review comment (FR-002).

Note: this feature has no use cases, so `LexTime.Application` contains only its
`AddLexTimeApplication()` registration extension and its `<Description>`. It is near-empty
for two features — the reporting interface lands in it with feature 003. P4 states the
four projects unconditionally, so creating it now is compliance, not anticipation.

### P7 — Stored procedures source-controlled, never created by a migration

**PASS.** `db/programmability/` is created and left empty (FR-018); the first procedure
arrives with feature 003 and the mechanism that applies them arrives with feature 002. The
obligation this feature carries is negative and verifiable: no migration in this feature
contains procedure DDL, and nothing in the model configuration emits any.

The CA2100 consequence of applying procedure files from disk belongs to feature 002, where
that code is written.

### P13 — Coverage is deliberate, not uniform *(SHOULD)*

**PASS, no departure.** P13 directs effort at domain rules and the reporting path. This
feature has neither — its rules live in the schema and no reporting exists. Tests
therefore cover exactly what this feature asserts (FR-029):

- The duration `CHECK` rejects zero, negative, non-multiples of six and values over 1440,
  and accepts valid ones.
- Uniqueness: duplicate client codes and duplicate emails rejected; the same matter
  number accepted under two different clients and rejected twice under one.
- A three-year-old billing date is accepted — the negative test that proves FR-012, and
  the one most likely to be broken by someone "fixing" the missing date constraint.
- Health returns 200 with the database check named when reachable, 503 naming it when
  not.
- Absent, malformed, expired and wrongly-signed tokens each return 401; a validly signed
  token is accepted.

### P25 — Everything documented in XML doc comments

**PASS, with a cost stated rather than discovered.** `Directory.Build.props` sets
`GenerateDocumentationFile=true`, turning on CS1591; `--warnaserror` in the pipeline makes
an undocumented public member a build failure.

This applies to `LexTime.IntegrationTests` as well. P25 exempts nothing, so every `[Fact]`
needs a `<summary>`. That is roughly a dozen comments here and it is accepted, not waived
— a summary stating which requirement a test pins is more useful than the method name
alone. If it becomes friction at scale the fix is amending P25 with an explicit
test-project clause, not a `GenerateDocumentationFile=false` line in one `.csproj`.

SC-003 verifies the gate by deliberately adding an undocumented member once and confirming
the build fails.

### Remaining principles

| Principle | Verdict | Note |
|---|---|---|
| P1 hiring signal | PASS | The dependency direction is verifiable from the `.csproj` files in under a minute |
| P2 PRD out-of-scope binding | PASS | No repository abstraction, no mediator, no mapper, no frontend |
| **P3 one evening per spec** | **PASS** | This is the split that resolved the original failure. Scope is projects, model, migration, compose file, auth, health, test harness |
| P5 right tool per access path | PASS | EF Core owns the schema and writes. No reporting path exists yet |
| P6 rules in domain and database | PASS (storage half) | FR-011. The application half is feature 004 and is named out of scope |
| P8 measured, not asserted | PASS | No performance claim made. FR-014 withholds the covering index so feature 003 has a baseline |
| P9 realistic seed shape | N/A | Feature 002 |
| P10 rollup is the headline | N/A | Feature 003 |
| P11 real SQL Server in tests | PASS | Testcontainers; FR-028 forbids any in-memory or file-based provider reference |
| P12 hand-computed rollup fixture | N/A | Feature 003 |
| P14 spec before code | PASS | Spec and plan precede any implementation task |
| P15 generated SQL reviewed | PASS | The migration's generated DDL and constraint expressions are read manually before commit |
| P16 agent mistakes logged | PASS | `docs/agent-log.md` is created here, since it is first referenced by this feature's work |
| P17 separate commits | PASS | Spec, plan and implementation are separate commits |
| P18 quickstart works cold | PARTIAL, by design | This feature leaves three manual commands. Feature 002 reduces them to the two the README promises; until then the README's quickstart section describes the state accurately rather than aspirationally |
| P19 trade-offs stated | PASS | The symmetric development key and absent secret management are already named in the README |
| P20 English | PASS | |
| P21 composition roots are extension methods | PASS | One registration extension per layer (FR-004) |
| P22 branch per spec | PASS | `001-solution-and-schema` |
| P23 reproducible quality gate | PASS | `Directory.Build.props` and `.editorconfig` land here and cover every later feature |
| P24 security review on auth and SQL | PASS | One review item: the token validation configuration — key source, algorithm restriction, lifetime and audience validation, all of which have insecure defaults worth checking by hand |

**Gate result: pass.** No MUST is violated and no SHOULD requires a departure.

The one honest caveat is P18. This feature cannot satisfy "the quickstart is two commands
and it works from cold" because the script that makes it two commands is feature 002. The
compliant response is that the README describes the current state truthfully in the
interim; claiming the two-command quickstart before it exists would be the actual
violation.

## Project Structure

### Documentation (this feature)

```text
specs/001-solution-and-schema/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── health.md        # GET /health request and response contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
global.json                          # SDK pinned to 9.0.317 (already committed)
Directory.Build.props                # analyzer, nullable, docs settings for every project
.editorconfig                        # per-rule severities
docker-compose.yml                   # SQL Server 2022 with a persistent volume
LexTime.sln

db/
└── programmability/                 # created empty; filled in feature 003

src/
├── LexTime.Api/                     # Program.cs, health endpoint, JWT wiring, placeholder route
├── LexTime.Application/             # AddLexTimeApplication() only in this feature
├── LexTime.Domain/                  # User, Client, Matter, TimeEntry — references nothing
└── LexTime.Infrastructure/          # LexTimeDbContext, entity configurations, migrations,
                                     # AddLexTimeInfrastructure()

tests/
└── LexTime.IntegrationTests/        # Testcontainers fixture, constraint tests, health tests, auth tests
```

**Structure Decision**: Four projects under `src/` with one test project under `tests/`,
exactly as constitution P4 requires and `docs/prd.md` §5 documents. The template's
single-project and frontend/backend options do not apply — there is no frontend
(`docs/prd.md` §2.2) and the layered split is a constitutional requirement rather than a
choice available to this plan.

## Complexity Tracking

> Filled only where the Constitution Check found a departure requiring justification.

No departures. The P3 failure that prompted this split is resolved by the split itself
rather than by a waiver, which is what the constitution's Governance section requires for
a MUST.
