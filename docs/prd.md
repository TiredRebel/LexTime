# PRD — LexTime: Minimal Timekeeping API

**Status:** Draft v1.0
**Owner:** Dmytro
**Type:** Portfolio / interview demo repository — not a product
**Budget:** 3–4 evenings (~16–20 focused hours) — raised from 2–3 when
constitution v2.0.0 added the Application layer; see §7

---

## 1. Problem

Law firms bill in six-minute increments. A timekeeper's day is fragmented across
many matters for many clients, and the firm's revenue depends entirely on that
fragmented record being captured accurately and rolled up correctly. The core
backend problem is not CRUD — it is the reporting path: aggregating hundreds of
thousands of small time entries into per-client, per-week billable totals fast
enough to power a dashboard, without dragging the write path down.

This repository demonstrates that path end to end in .NET and SQL Server.

### Why this repo exists (the meta-problem)

The author has 15+ years of .NET/C# and shipped in product organisations
(Microsoft Dynamics 365 R&D, Intel, CoreLogic), but spent the last year on
Python data pipelines. A reviewer's fair question is "are you still current?"
This repository answers it with a small, real, runnable artifact rather than a
paragraph in a cover letter, and it targets exactly the stack in the job
description: .NET/C#, REST API, SQL Server, stored procedures, PowerShell,
Azure DevOps.

### Reviewer questions the repo must answer

| Question | Where it is answered |
| --- | --- |
| Can they still write idiomatic modern .NET? | `src/LexTime.Api` — .NET 9 minimal API, typed results, DI, validation |
| Do they structure a solution deliberately? | Clean architecture across four projects, dependency direction enforced by project references, no framework doing the structuring for them — §5 |
| Do they know SQL beyond `SELECT *`? | `usp_WeeklyBillableRollup` — window functions, execution plans, index tuning with real numbers |
| Do they know when *not* to use the ORM? | EF Core for CRUD, raw ADO.NET / `SqlCommand` for reporting; rationale documented |
| Can they test against a real database? | Testcontainers integration tests against real SQL Server 2022 |
| Can they ship? | `azure-pipelines.yml`, PowerShell bootstrap script |
| Can they work with an AI agent professionally? | `CLAUDE.md`, Spec Kit specs, README section on what the agent got wrong |

---

## 2. Scope

### 2.1 In scope

**Domain**

- Users (timekeepers) — seeded, read-only via API
- Clients — CRUD
- Matters (a matter belongs to exactly one client) — CRUD
- Time entries — full CRUD, with domain rules

**Domain rules (deliberately few, but real)**

1. Duration is stored in **minutes** and must be a positive multiple of **6**
   (0.1 billable hour). Non-conforming values are rejected with `400`.
2. A single time entry may not exceed **1440** minutes.
3. Total logged minutes per user per `WorkDate` may not exceed **1440**.
4. `WorkDate` may not be in the future and may not be more than **90 days** in
   the past (a stand-in for a period-close rule).
5. Time entries may only be created against an **active** matter of an
   **active** client.
6. `HourlyRate` is snapshotted onto the time entry at creation from the user's
   current rate — rate changes do not retroactively rewrite history.

**Reporting**

- One stored procedure, `dbo.usp_WeeklyBillableRollup`, is the headline
  artifact. Per ISO week and client it returns: billable hours, non-billable
  hours, billable amount, a running cumulative billable total per client, and a
  dense rank of clients by billable hours within each week. Implemented with
  `SUM() OVER (PARTITION BY ... ORDER BY ...)`, `LAG()` and `DENSE_RANK()`.
- Called directly via `SqlCommand`/`SqlDataReader`, not through EF Core.

**Infrastructure**

- .NET SDK pinned to **9.0.317** via `global.json` with
  `"rollForward": "latestFeature"`. Pinned because the authoring machine also has
  10.0.302 installed and would otherwise pick it silently; `latestFeature` rather
  than `disable` so a reviewer on any 9.0.x still satisfies the §6 cold start.
- SQL Server 2022 in Docker Compose
- EF Core 9 code-first migrations for tables; stored procedures kept as
  idempotent `.sql` files applied by the bootstrap script
- PowerShell script that brings up the container, applies migrations, applies
  stored procedures, and seeds a realistic dataset
- xUnit integration tests on Testcontainers against real SQL Server
- Code quality enforced by the SDK's built-in Roslyn analyzers — no third-party
  package and no commercial tool. One root `Directory.Build.props` sets
  `AnalysisMode=Recommended`, `AnalysisModeSecurity=All`,
  `EnforceCodeStyleInBuild=true`, `Nullable=enable`, `NuGetAudit=true` and
  `GenerateDocumentationFile=true`; one root `.editorconfig` tunes individual
  rule severities. The pipeline builds with `--warnaserror`; local builds do not.
  `GenerateDocumentationFile` turns on CS1591, which is what makes constitution
  P25 a build failure rather than a convention.
- `azure-pipelines.yml`: restore/build → test → publish artifact

**Documentation**

- `README.md` with execution plans before and after the covering index, with
  actual `SET STATISTICS IO, TIME ON` numbers from a real run
- `CLAUDE.md` and Spec Kit specifications committed
- README section: how this was built with Claude Code, including what the agent
  generated incorrectly and how it was caught
- Graphify dependency graph + screenshot committed
- LLM Wiki pages for 3–4 key decisions

### 2.2 Explicitly out of scope

Listed here so the reviewer sees the boundary was chosen, not missed.

| Not building | Why |
| --- | --- |
| Any frontend / UI | The role is backend; a half-finished React app weakens the signal |
| Multi-tenancy, firm-level isolation | Real product concern, doubles the data model and every query |
| Invoicing, trust accounting, LEDES export | Real legal-billing domain, far past a demo |
| Approval / submit / lock workflow | Adds state machine and 3× the endpoints for no new technical signal |
| Rate cards, rate history, matter-level rates | One snapshot rate on the entry proves the point |
| Conflicts checking, ethical walls | Domain depth without engineering depth |
| Real identity provider, user registration, RBAC | JWT is validated with a symmetric dev key; the auth boundary is visible, the IdP is not the demo |
| Soft delete, full audit trail | `CreatedAtUtc`/`UpdatedAtUtc` only |
| Caching, rate limiting, background jobs | No load to justify them |
| Kubernetes, Terraform, real Azure deploy | Pipeline stops at `publish`; deploying to a live environment costs money and a weekend |
| OData | On the author's résumé, but it adds a whole query surface with no reporting story; noted in the README as a deliberate trade |
| More than one report | The pattern is proven once; repeating it is padding |
| Load/perf test harness (k6, NBomber) | The index before/after numbers carry the performance story |
| NDepend or any commercial quality tool | A reviewer cannot run it, so the report is an assertion rather than something reproducible — the same objection §6.6 makes about invented performance numbers. The SDK's built-in analyzers give a gate that runs in anyone's `dotnet build` |
| StyleCop.Analyzers | Considered as the free alternative to NDepend. Mandatory doc comments — its SA1600 rule — are now wanted (constitution P25), but the compiler already provides them: `GenerateDocumentationFile` turns on CS1591 for the same purpose with no package reference. What StyleCop would add beyond that is member ordering and using-directive placement, which the built-in code-style rules already cover. Rejected as redundant, not as noise |
| Generic repository over `DbSet<T>` | EF Core's `DbSet<T>` is already the repository and `DbContext` is already the unit of work; wrapping them adds a layer that only forwards calls |
| MediatR | Proposed with the layering, then dropped. Commercial licence from v13.0.0 (last Apache-2.0 release: v12.5.0), and v13+ requires a registered licence key at runtime — a reviewer would need a mediatr.io account before `dotnet run` was quiet, which breaks the two-command quickstart in §6.3. Independently of the licence, each handler has exactly one caller, so there is nothing to mediate. Replaced by handler classes in DI |
| AutoMapper | Same origin, same decision. Commercial licence from v15.0.0 (last MIT release: v14.x). Six DTOs mapped by `ToDto()` extension methods are checked at compile time; AutoMapper's failure mode is a runtime exception on a renamed property |

### 2.3 Non-goals of *quality*, stated honestly

This is a demo. It will have: no horizontal scaling story, no migration
rollback plan, no secret management beyond `appsettings.Development.json` and
pipeline variables, and test coverage concentrated on the reporting path and
domain rules rather than uniform across the codebase. The README says so
out loud rather than pretending otherwise.

Two more, named here so they are trade-offs rather than discoveries:

- **The layering costs more than it saves at this size.** Seventeen endpoints,
  four of which carry real logic, do not need a handler class each. The
  layering is here because clean architecture is worth showing done properly;
  that is a presentation decision, and the README states it as one rather than
  claiming the design fell out of the problem.
- **Zero third-party runtime dependencies in `Application`.** The layer is
  project references, handler classes and extension methods. That is a
  deliberate answer to the licence changes across the .NET ecosystem, and it
  means the quickstart in §6 needs no account, key or licence file.

---

## 3. Data model

Four tables. Schema `dbo`.

### Users

| Column | Type | Notes |
| --- | --- | --- |
| `UserId` | `int IDENTITY` | PK |
| `Email` | `nvarchar(256)` | Unique |
| `FullName` | `nvarchar(200)` | |
| `DefaultHourlyRate` | `decimal(10,2)` | USD |
| `IsActive` | `bit` | |
| `CreatedAtUtc` | `datetime2(3)` | |

### Clients

| Column | Type | Notes |
| --- | --- | --- |
| `ClientId` | `int IDENTITY` | PK |
| `ClientCode` | `nvarchar(20)` | Unique, e.g. `ACME` |
| `Name` | `nvarchar(200)` | |
| `IsActive` | `bit` | |
| `CreatedAtUtc` | `datetime2(3)` | |

### Matters

| Column | Type | Notes |
| --- | --- | --- |
| `MatterId` | `int IDENTITY` | PK |
| `ClientId` | `int` | FK → `Clients`, indexed |
| `MatterNumber` | `nvarchar(30)` | Unique within client |
| `Name` | `nvarchar(250)` | |
| `IsBillableByDefault` | `bit` | |
| `IsActive` | `bit` | |
| `CreatedAtUtc` | `datetime2(3)` | |

### TimeEntries

| Column | Type | Notes |
| --- | --- | --- |
| `TimeEntryId` | `bigint IDENTITY` | PK, clustered |
| `UserId` | `int` | FK → `Users` |
| `MatterId` | `int` | FK → `Matters` |
| `WorkDate` | `date` | The billing date, not the entry date |
| `DurationMinutes` | `int` | `CHECK (DurationMinutes > 0 AND DurationMinutes % 6 = 0 AND DurationMinutes <= 1440)` |
| `IsBillable` | `bit` | |
| `HourlyRateSnapshot` | `decimal(10,2)` | Copied from user at creation |
| `Narrative` | `nvarchar(1000)` | |
| `CreatedAtUtc` | `datetime2(3)` | |
| `UpdatedAtUtc` | `datetime2(3)` | Nullable |

**Indexes.** Ships with only the PK and FK indexes on day one, *on purpose* —
the README's before/after story adds:

```sql
CREATE NONCLUSTERED INDEX IX_TimeEntries_WorkDate_Billable
    ON dbo.TimeEntries (WorkDate, IsBillable)
    INCLUDE (MatterId, DurationMinutes, HourlyRateSnapshot);
```

**Seed volume.** 25 users, 60 clients, ~220 matters, ~400,000 time entries
across 24 months. Large enough that the missing index is visibly painful and
the plan shapes differ, small enough to seed in well under a minute via
`SqlBulkCopy`.

---

## 4. API surface

Base path `/api/v1`. JSON only. JWT bearer required on everything except
`/health` and `/swagger`. `ProblemDetails` (RFC 7807) for all errors.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Liveness + DB connectivity |
| `GET` | `/api/v1/users` | List timekeepers |
| `GET` | `/api/v1/users/{userId}` | Single timekeeper |
| `GET` | `/api/v1/clients` | List clients (`?isActive=`, `?skip=`, `?take=`) |
| `POST` | `/api/v1/clients` | Create client |
| `GET` | `/api/v1/clients/{clientId}` | Single client |
| `PUT` | `/api/v1/clients/{clientId}` | Update client |
| `GET` | `/api/v1/clients/{clientId}/matters` | Matters for a client |
| `POST` | `/api/v1/clients/{clientId}/matters` | Create matter |
| `GET` | `/api/v1/matters/{matterId}` | Single matter |
| `PUT` | `/api/v1/matters/{matterId}` | Update matter |
| `GET` | `/api/v1/time-entries` | List (`?userId=`, `?matterId=`, `?from=`, `?to=`, `?skip=`, `?take=`) |
| `POST` | `/api/v1/time-entries` | Create — enforces all six domain rules |
| `GET` | `/api/v1/time-entries/{id}` | Single entry |
| `PUT` | `/api/v1/time-entries/{id}` | Update |
| `DELETE` | `/api/v1/time-entries/{id}` | Hard delete |
| `GET` | `/api/v1/reports/weekly-billable-rollup` | **The headline endpoint** — `?from=`, `?to=`, optional `?clientId=`. Calls `dbo.usp_WeeklyBillableRollup` |

Seventeen endpoints. Sixteen are a health check and plain CRUD. Under the
layering in §5 each of them costs a handler class and a `ToDto()` extension
rather than a single minimal-API lambda, so they are generated in bulk but
reviewed in bulk too — §7 names them as the first thing cut if evening 2
overruns. The engineering attention goes to the last one.

### Rollup response shape

```jsonc
{
  "from": "2026-01-05",
  "to": "2026-03-29",
  "rows": [
    {
      "isoYear": 2026,
      "isoWeek": 3,
      "weekStartDate": "2026-01-12",
      "clientId": 7,
      "clientCode": "ACME",
      "clientName": "Acme Holdings",
      "billableHours": 128.4,
      "nonBillableHours": 11.2,
      "billableAmount": 48150.00,
      "cumulativeBillableHours": 371.9,   // SUM() OVER (PARTITION BY client ORDER BY week)
      "hoursDeltaVsPriorWeek": 14.7,      // LAG()
      "clientRankInWeek": 2               // DENSE_RANK()
    }
  ]
}
```

---

## 5. Repository layout

```
/
├─ CLAUDE.md
├─ README.md
├─ azure-pipelines.yml
├─ docker-compose.yml
├─ global.json                     # pins the SDK to 9.0.317
├─ Directory.Build.props           # analyzer settings for every project
├─ .editorconfig                   # per-rule severities
├─ .specify/                      # Spec Kit: constitution, specs, plans, tasks
├─ docs/
│  ├─ prd.md
│  ├─ performance.md              # plans + STATISTICS IO/TIME, before and after
│  ├─ agent-log.md                # what Claude Code got wrong and how it was caught
│  └─ graphify/                   # dependency graph + screenshot
├─ scripts/
│  └─ Initialize-LocalDb.ps1      # up, migrate, apply sprocs, seed
├─ db/
│  └─ programmability/
│     └─ usp_WeeklyBillableRollup.sql
├─ src/
│  ├─ LexTime.Api/                # endpoints, validation, auth, DI composition
│  ├─ LexTime.Application/        # one handler class per use case, DTOs + ToDto()
│  │                              # extensions, interfaces implemented by Infrastructure
│  ├─ LexTime.Domain/             # entities + domain rules, references nothing
│  └─ LexTime.Infrastructure/     # EF Core DbContext, migrations, sproc client
└─ tests/
   └─ LexTime.IntegrationTests/   # Testcontainers + xUnit
```

Four projects, clean architecture, dependencies pointing inward: `Api` →
`Application` → `Domain`, with `Infrastructure` depending on `Domain` and
plugged into `Application` through interfaces `Application` declares. Use cases
are plain handler classes registered in DI; DTO mapping is a `ToDto()`
extension method beside each DTO. No mediator library, no mapping library — see
§2.2.

The layering is deliberate signal: it is enforced by project references rather
than left to be inferred from folder names, so a reviewer can verify the
dependency direction by opening the `.csproj` files rather than by trusting the
folder names. Constitution P4 is the binding statement of this rule.

---

## 6. Done criteria

The repository is done when every one of these is true and verifiable by a
reviewer who has only Docker and the .NET 9 SDK.

**Runs**

1. `pwsh ./scripts/Initialize-LocalDb.ps1` completes from a cold machine and
   leaves a seeded database; the script is idempotent on a second run.
2. `dotnet run --project src/LexTime.Api` serves Swagger UI and a green
   `/health`.
3. `README.md` quickstart is exactly these two commands — no undocumented step.

**Correct**

4. `dotnet test` is green and includes, at minimum:
   - all six domain rules, each with a rejecting and an accepting case;
   - a rollup test asserting cumulative totals, `LAG` delta and `DENSE_RANK`
     against a hand-computed fixture;
   - a rollup test over an empty date range and over a week with zero billable
     hours (the boundary cases where window functions usually break).
5. Tests run against real SQL Server via Testcontainers — no in-memory or
   SQLite provider anywhere in the test project.

**Fast, and shown to be**

6. `docs/performance.md` contains, for the rollup over the full seeded range:
   pre-index and post-index execution plans, actual `STATISTICS IO` logical
   reads, actual `STATISTICS TIME` elapsed ms, and one paragraph naming the
   plan-shape change (expected: scan + sort + hash aggregate → index seek/scan
   + stream aggregate, and the sort disappearing). Real captured numbers, no
   placeholders, no invented figures.

**Ships**

7. `azure-pipelines.yml` defines build → test → publish, uses a Microsoft-hosted
   `ubuntu-latest` pool, publishes test results and a web deploy artifact, and
   is syntactically valid.
8. The pipeline builds with `--warnaserror`, so any analyzer diagnostic fails
   the build. A reviewer running `dotnet build` on a clean checkout gets the
   same diagnostics from the same `Directory.Build.props` and `.editorconfig` —
   no tool to install, no licence. Because `GenerateDocumentationFile` is on,
   this includes CS1591: a public member without an XML doc comment fails the
   build. Comments that merely restate the signature pass the compiler and are
   caught in review instead (P25).

**Explains itself**

9. `README.md` has a "Built with Claude Code" section naming at least three
   concrete things the agent got wrong (with the actual symptom and how it was
   caught — review, failing test, or plan inspection), not a generic
   "AI-assisted" note.
10. `CLAUDE.md` and the Spec Kit constitution/specs are committed and were
    actually used, visible in commit history.
11. Graphify graph and screenshot committed under `docs/graphify/`.
12. LLM Wiki pages exist for: EF Core vs. stored procedures for reporting; the
    index and plan-shape decision; the Testcontainers testing strategy; the
    spec-driven workflow with Claude Code.

---

## 7. Time budget

| Evening | Work |
| --- | --- |
| **1** | Spec Kit `constitution.md` + `/specify` + `/plan`; four-project scaffold with `Directory.Build.props`, `.editorconfig`, `global.json`; EF model + migrations; `docker-compose.yml`; `Initialize-LocalDb.ps1` with `SqlBulkCopy` seeding |
| **2** | Handler classes and `ToDto()` extensions for the CRUD surface; endpoints + JWT + validation; `usp_WeeklyBillableRollup` and its sproc call path behind an `Application` interface |
| **3** | Testcontainers harness + rollup and domain-rule tests; capture plans and `STATISTICS IO/TIME` before and after the index; `azure-pipelines.yml` |
| **4** | README including the performance, trade-offs and Claude Code sections; Graphify; LLM Wiki pages; final pass |

The layering added in constitution v2.0.0 pushed this from three evenings to
four. Constitution P3 caps a single *spec* at roughly one evening, not the
project, so the split above keeps P3 satisfied — but P3 also governs the tie:
if the scaffold overruns, scope is cut, the layering is not abandoned
mid-build.

The generated CRUD handlers are the cut candidate. If evening 2 overruns, the
sixteen plain endpoints drop to the minimum that exercises each domain rule and
the rollup keeps its full attention (P10). After that the cut order is: LLM
Wiki pages → Graphify → README polish. The performance section and the pipeline
are not cuttable — they are the two things the job description asks for by
name.

---

## 8. Risks

| Risk | Mitigation |
| --- | --- |
| Testcontainers SQL Server image is slow to pull/start in CI | Pull is cached locally; if the hosted agent is too slow, tests run against a `services:` SQL Server container in the pipeline instead, documented as such |
| Index makes less difference than expected at 400k rows | Seed volume is a tuning knob; raise to 1–2M rows if the delta is not clearly visible. Report whatever the real numbers are — an honest small delta beats a fabricated large one |
| Scope creep into invoicing / approval workflow | §2.2 is the contract; the Spec Kit constitution repeats it as a hard rule |
| Agent generates plausible-but-wrong SQL for window functions | The hand-computed rollup fixture in test exists specifically to catch this, and whatever it catches goes into the README |
