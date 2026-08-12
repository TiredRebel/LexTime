# LexTime

A minimal timekeeping API for legal billing — .NET 9, SQL Server 2022, and one
stored procedure that does the interesting work.

> **Status: specification complete, implementation not started.**
> The constitution (`.specify/memory/constitution.md`) and PRD (`docs/prd.md`)
> are committed and binding. Sections below marked `TODO(measure)` are
> deliberately empty — this repository's constitution forbids publishing
> performance numbers that were not captured from a real run, so the
> placeholders stay visibly empty until they are.

---

## What this is

Law firms bill in six-minute increments. A timekeeper's day fragments across
many matters for many clients, and firm revenue depends on that fragmented
record rolling up correctly. The hard part is not CRUD — it is aggregating
hundreds of thousands of small time entries into per-client, per-week billable
totals fast enough to power a dashboard, without slowing the write path.

This repository demonstrates that path end to end. It is a portfolio artifact,
not a product; `docs/prd.md` §2.2 lists what it deliberately does not build.

## Quickstart

Requires Docker and the .NET SDK 9.0.317 or a later 9.0.x (pinned in
`global.json`). Two commands:

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
dotnet run --project src/LexTime.Api
```

The first brings up SQL Server in Docker, applies EF Core migrations, applies
the stored procedures, and seeds ~400,000 time entries via `SqlBulkCopy`. It is
idempotent — running it twice is safe. The second serves Swagger UI and a
`/health` endpoint that checks database connectivity.

No account, no licence key, no tool to install. If a step exists but is not
documented here, that is a defect.

## Architecture

Four projects, dependencies pointing inward, enforced by project references —
open the `.csproj` files and the direction is verifiable rather than implied by
folder names:

```
LexTime.Api  →  LexTime.Application  →  LexTime.Domain
                                             ↑
                       LexTime.Infrastructure ┘
```

- **`LexTime.Domain`** — entities and the six domain rules. References nothing.
- **`LexTime.Application`** — one handler class per use case, DTOs with their
  `ToDto()` extensions, and the interfaces `Infrastructure` implements.
- **`LexTime.Infrastructure`** — EF Core `DbContext`, migrations, and the
  stored-procedure client.
- **`LexTime.Api`** — endpoints, validation, JWT, DI composition. An endpoint
  validates input, invokes a handler, and maps the result to a status code.

`Application` has **no third-party runtime dependencies**. No mediator library:
each handler has exactly one caller, so there is nothing to mediate. No mapping
library: six DTOs mapped by hand are checked at compile time, where a mapping
library's failure mode is a runtime exception on a property someone renamed.
Both were considered explicitly and are recorded in `docs/prd.md` §2.2 with the
reasoning, including their move to paid licensing.

### Two data access paths, on purpose

EF Core owns writes and simple entity reads. Reporting reads go through stored
procedures called directly with `SqlCommand`/`SqlDataReader` — never
`FromSqlRaw`, never mapped onto EF entities. The reporting handler depends on an
interface declared in `Application`; the raw ADO.NET lives behind it in
`Infrastructure` and nowhere else.

This split is the technical argument the repository exists to make. An ORM is
the right tool for writing one row with change tracking. It is the wrong tool
for a window-function aggregate over 400,000 rows.

### Domain rules

Enforced in C# with clear error messages, and — where expressible — again as
`CHECK` constraints in the schema. The duplication is intentional.

1. Duration is stored in minutes and must be a positive multiple of 6.
2. A single entry may not exceed 1440 minutes.
3. Total minutes per user per work date may not exceed 1440.
4. Work date may not be in the future, nor more than 90 days in the past.
5. Entries require an active matter belonging to an active client.
6. The hourly rate is snapshotted onto the entry at creation. Rate changes do
   not rewrite history.

## The rollup

`dbo.usp_WeeklyBillableRollup` is the headline. Per ISO week and client it
returns billable hours, non-billable hours, billable amount, a running
cumulative billable total per client, the delta against the prior week, and a
dense rank of clients within each week — via `SUM() OVER (PARTITION BY ...
ORDER BY ...)`, `LAG()` and `DENSE_RANK()`.

`GET /api/v1/reports/weekly-billable-rollup?from=&to=&clientId=`

```jsonc
{
  "isoYear": 2026,
  "isoWeek": 3,
  "clientCode": "ACME",
  "billableHours": 128.4,
  "cumulativeBillableHours": 371.9,   // SUM() OVER
  "hoursDeltaVsPriorWeek": 14.7,      // LAG()
  "clientRankInWeek": 2               // DENSE_RANK()
}
```

The procedure lives in `db/programmability/`, is written `CREATE OR ALTER`, and
is applied by the bootstrap script — never by an EF migration.

## Performance

The schema ships with only primary and foreign key indexes, on purpose, so the
before/after has something to show. The covering index under test:

```sql
CREATE NONCLUSTERED INDEX IX_TimeEntries_WorkDate_Billable
    ON dbo.TimeEntries (WorkDate, IsBillable)
    INCLUDE (MatterId, DurationMinutes, HourlyRateSnapshot);
```

| Metric | Before index | After index |
| --- | --- | --- |
| Logical reads | `TODO(measure)` | `TODO(measure)` |
| Elapsed (ms) | `TODO(measure)` | `TODO(measure)` |
| Plan shape | `TODO(measure)` | `TODO(measure)` |

Execution plans and raw `SET STATISTICS IO, TIME ON` output go in
`docs/performance.md`. If the measured improvement turns out to be
unimpressive, the unimpressive number is what gets published.

## Testing

xUnit against real SQL Server 2022 via Testcontainers. No in-memory provider,
no SQLite, no mocked `DbContext` — a test that cannot run against the real
engine is not testing what this repository claims to be good at.

Coverage is deliberate rather than uniform: every domain rule gets a rejecting
and an accepting case, the rollup is asserted against a **hand-computed
fixture** (expected cumulative totals, `LAG` deltas and ranks worked out by a
human, not by running the procedure and recording its output), and boundary
cases — empty date ranges, weeks with zero billable hours — are covered
explicitly. Trivial CRUD gets one happy path and one 404. No coverage
percentage is targeted or reported.

```powershell
dotnet test
```

## Build, documentation and the quality gate

Code quality comes from the SDK's built-in Roslyn analyzers — no third-party
analyzer package, no commercial tool. `Directory.Build.props` sets
`AnalysisMode=Recommended`, `AnalysisModeSecurity=All`,
`EnforceCodeStyleInBuild=true`, `Nullable=enable`, `NuGetAudit=true` and
`GenerateDocumentationFile=true`; `.editorconfig` tunes individual severities.

Everything is documented in XML doc comments — types, methods, properties,
parameters, return values, thrown exceptions, private members included — and
each project states its purpose in `<Description>` in its `.csproj`.
`GenerateDocumentationFile` turns on CS1591, so an undocumented public member is
a compiler diagnostic rather than a convention someone has to remember.

A comment that restates its signature is treated as a defect, not as
compliance. `/// <summary>Gets the client id.</summary>` is rejected in review
the same as a missing comment; a summary is expected to say what the caller is
responsible for, what the units are, or what happens at the boundaries.

The pipeline builds with `--warnaserror`, so any diagnostic — analyzer or
missing documentation — fails the build. Local builds do not, so a warning
blocks the merge without blocking the edit-run loop. Everything the gate reports
is reproducible by running `dotnet build` on a clean checkout.

`azure-pipelines.yml`: restore/build → test → publish. It stops at `publish`.

## Trade-offs

Stated because unstated shortcuts read as oversights and stated ones read as
judgement.

| Shortcut | Why |
| --- | --- |
| JWT validated with a symmetric dev key | The auth boundary is the demo; the identity provider is not |
| No multi-tenancy | Real concern, but it doubles the data model and every query |
| Hard deletes, `CreatedAtUtc`/`UpdatedAtUtc` only | A full audit trail is product work, not signal |
| Pipeline stops at publish | A live Azure environment costs money and a weekend |
| No OData | On my résumé, but it adds a query surface with no reporting story |
| One report, not several | The pattern is proven once; repeating it is padding |
| No secret management beyond `appsettings.Development.json` | Demo scope, said out loud rather than hidden |
| Clean-architecture layering at this size | Seventeen endpoints, four with real logic, do not need a handler class each. The layering is here because it is worth showing done properly — a presentation decision, not one the problem forced |

## Built with Claude Code

Spec-driven, via Spec Kit: the constitution, specs and plans are committed, and
the history shows spec → plan → implementation as separate commits on a branch
per feature.

A repository claiming AI-assisted development with zero friction is not
credible, so here is the friction. Full log in `docs/agent-log.md`.

**1. It wrote a file into the wrong repository.** Asked to copy the PRD to
`docs/prd.md`, the agent ran the copy in a shell whose working directory had
silently reset to a different project between calls. The file landed in an
unrelated Python repo. Caught by running `git status` and seeing a new file
appear in a repository that should have been clean — the agent had already
"verified" the copy with a bare `git status`, which reported success from the
wrong directory. Fixed by giving every subsequent git and file call an absolute
path.

**2. It wrote an unverified command-line flag into a binding principle.** The
constitution mandates that the pipeline build with `--warnaserror`. That flag
was asserted from recollection before it was ever run. On testing, the first
attempt appeared to pass with zero warnings — because the build was incremental
and nothing recompiled. Only `--no-incremental` proved the flag actually
escalates a warning to an error and returns exit 1. The flag was real; the test
that first "confirmed" it proved nothing.

**3. It wrote a rule that its own next action would violate.** The first draft
of the branching principle read "`main` is never committed to directly." The
constitution's amendment clause requires amendments in a dedicated commit with
no branch involved, and the very next commit was an amendment on `main`. Caught
in review before the principle was committed, then narrowed to exempt
governance commits.

**4. It stated third-party licence terms from memory.** When the layering was
first specified with MediatR and AutoMapper, the agent wrote their commercial
licensing into the PRD as fact, sourced from recall. Checking the actual
packages changed the decision: both moved to paid licences (MediatR from
v13.0.0, AutoMapper from v15.0.0), and MediatR v13+ requires a **registered
licence key at runtime** — which would have put a mediatr.io signup in front of
the two-command quickstart. That detail was not in the recalled version and is
the reason both libraries were dropped. The constitution's rule against
unverified performance numbers turned out to apply just as well to licences.

## Layout

```
├─ db/programmability/     usp_WeeklyBillableRollup.sql
├─ docs/                   prd.md, performance.md, agent-log.md, graphify/
├─ scripts/                Initialize-LocalDb.ps1
├─ src/                    Api, Application, Domain, Infrastructure
├─ tests/                  LexTime.IntegrationTests
└─ .specify/               constitution, specs, plans, tasks
```

## Licence

MIT — see [LICENSE](LICENSE).
