# LexTime

A minimal timekeeping API for legal billing — .NET 9, SQL Server 2022, and one
stored procedure that does the interesting work.

> **Status: features 001–009 complete — solution, schema, seeded data, health,
> access boundary, the weekly billable rollup, its measured index, the time-entry
> write path, the client, matter and timekeeper API surface, and a thin browser
> consumer of the rollup, time entries, and party directories.** The four projects
> build clean under `--warnaserror`, the two-command quickstart works from cold,
> 400,000 deterministic time entries load in under a minute, and 131 tests run against
> a real SQL Server container. The performance figures below are captured from a run
> you can repeat with one command.

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
```

```powershell
dotnet run --project src/LexTime.Api
```

The first brings up SQL Server 2022, waits for it to accept queries, applies the
schema and any stored procedures, seeds 400,000 deterministic time entries,
verifies their distribution, and prints a development bearer token. It is
idempotent — a second run reports what it skipped and changes nothing. The
second command serves the dashboard at `http://localhost:5202/`, Swagger UI at
`http://localhost:5202/swagger`, and a `/health` endpoint that reports each
check by name. Paste the printed token into the dashboard token field or the
Swagger authorize box. Time entries is `#time-entries` in the same shell;
Clients is `#clients`; Timekeepers is `#timekeepers`; Reports remains the
weekly rollup. Matters are listed on the selected client, not as a firm-wide
table.

To rebuild the data without restarting the container:

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1 -Reset
```

To discard the container and its storage entirely:

```powershell
docker compose down -v
```

**No account, no licence key, nothing to install.** Migrations are applied by
the application itself rather than by the `dotnet-ef` global tool, specifically
so that this list stays at Docker and the SDK. If a step exists but is not
documented here, that is a defect.

The dashboard is a thin, static Next.js consumer of the existing authenticated
endpoints — the weekly rollup, the time-entry listing/write path, and the party
directories — not a second application or the repository's primary hiring signal.
Node is not required for the quickstart. It is needed only when changing files
under `web/` and regenerating the committed export:

```powershell
cd web
npm ci
npm run build
```

The build replaces `src/LexTime.Api/wwwroot/` deterministically; `dotnet build`
does not invoke npm.

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
  `ToDto()` extensions, and the interfaces `Infrastructure` implements. It owns
  the client, matter, timekeeper and time-entry use cases plus the reporting ports.
- **`LexTime.Infrastructure`** — EF Core `DbContext`, migrations, and the
  stored-procedure client.
- **`LexTime.Api`** — endpoints, validation, JWT, DI composition, and the
  committed static dashboard export. An endpoint validates input, invokes a
  handler, and maps the result to a status code.

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

All six live in one place — `LexTime.Domain/Rules/TimeEntryRuleSet.cs` — as a pure
function of a facts record. Four of them need data the domain cannot fetch for
itself (the day's other minutes, today's date, three active flags), so the
domain states what it needs and is told. **No handler and no endpoint restates a
limit**: a rule in two places is a rule that will eventually disagree with
itself and say nothing about it.

That purity is what makes the rule tests exhaustive and fast — every rule
refusing, every rule accepting, every boundary, in 30ms with no database.

Three details worth knowing:

- **Rules 1 and 2 are also `CHECK` constraints, still.** A test writes a
  violating row *outside* the application and asserts the database refuses it —
  so deleting the constraint because "the application checks it now" fails a
  test rather than passing review.
- **Rule 3 is a read-then-write**, and would otherwise be defeatable by timing:
  two requests both read a total of 1,400, both add 40, both pass, the day ends
  at 1,480. The read and the write share a serialisable transaction, and a test
  fires two concurrent submissions to prove exactly one survives.
- **Rule 6 has no refusing test**, because no submission can violate it. It is
  enforced by the update command having no rate field at all. Its accepting test
  — record, change the timekeeper's rate, revise the entry, assert the captured
  rate is untouched — is the one that catches a handler which rebuilt the entity
  and re-read the current rate, rewriting history on every edit.

**On update, the rules are field-scoped.** Rules 1, 2, 3 and 6 always re-apply;
the backdating window and the active-matter check apply only to fields actually
being changed. An entry recorded 200 days ago can still have its narrative
corrected and still cannot have its date moved. Validating the whole entry as if
newly recorded would freeze every old entry including its typos; skipping both
would let rule 5 be defeated by editing rather than creating.

## The rollup

`dbo.usp_WeeklyBillableRollup` is the headline. Per ISO week and client it
returns billable hours, non-billable hours, billable amount, a running
cumulative billable total per client, the delta against the prior week, and a
dense rank of clients within each week — via `SUM() OVER (PARTITION BY ...
ORDER BY ...)`, `LAG()` and `DENSE_RANK()`.

`GET /api/v1/reports/weekly-billable-rollup?from=&to=&clientId=`

The browser dashboard pages the returned rows locally at 20, 50, or 100 per
page. Page size and client filtering do not change the request, recompute
standing, or alter the period totals. Time entries is a second view in the
same shell: it pages `GET /api/v1/time-entries` with `skip`/`take` against the
matching total and does not send `clientId`. Clients and Timekeepers page the
existing party listings the same way; there is no firm-wide matters table and
no timekeeper write.

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

### Two questions it had to answer out loud

Both are the kind that produce a plausible report either way, so the spec states
a position and a test pins it.

**A client who is inactive today still appears for the weeks it billed in.**
Deactivation is forward-looking. A report on a past period describes what was
billed then, and a client that left last year still has last year's revenue. The
seed guarantees such clients exist, so the rule is testable rather than
theoretical.

**"The prior week" means the preceding *calendar* week, not the client's previous
row.** A client that bills, goes quiet for three weeks, then bills again is
compared against the silent week immediately before it — so the change is that
week's hours in full, not the difference against the week it last billed in.
Rows are still emitted only for weeks with activity; the gap is *detected*, not
filled with zero rows, because materialising them would return 6,240 rows
regardless of activity and collapse the ranking into a mass tie at zero.

`null` in `hoursDeltaVsPriorWeek` means the preceding week falls outside the
requested range. It does not mean zero, and coalescing it to zero misreports the
first week of every report.

### Weeks are identified by a day count, not a week number

The obvious encoding — `isoYear * 100 + isoWeek` — is wrong every January and
right the rest of the year. ISO week 1 of 2026 begins on Monday 29 December
2025, and the week before it is 2025 week 52, so "last week" cannot be found by
subtracting one from the week number.

The procedure keys on `DATEDIFF(day, '19000101', WorkDate) / 7` instead. 1900-01-01
was a Monday, so the ordinal increments by exactly one per calendar week across
year boundaries, and — unlike `DATEPART(weekday, …)` — it does not shift with the
caller's `SET DATEFIRST`. `LAG()` supplies the candidate previous row and a
comparison against `ordinal - 1` decides whether that candidate is really last
week or something older.

A test covers exactly this intersection: a gap spanning New Year, where week-number
arithmetic reports `+5.00` and the correct answer is `−3.00`. Neither a plain gap
test nor a plain year-attribution test catches it alone.

## Performance

The schema ships with only primary and foreign key indexes, on purpose, so the
before/after has something to show. The covering index under test:

```sql
CREATE NONCLUSTERED INDEX IX_TimeEntries_WorkDate_Billable
    ON dbo.TimeEntries (WorkDate, IsBillable)
    INCLUDE (MatterId, DurationMinutes, HourlyRateSnapshot);
```

Measured over the full 24-month seeded range, five readings per state, buffer
pool cleared before each:

| Metric | Before index | After index |
| --- | --- | --- |
| Logical reads | 6,879 | **1,768** |
| Elapsed, median | 132 ms | 112 ms |
| **CPU time** | **847 ms** | **105 ms** |
| Plan shape | clustered index scan, parallel, 4 sorts | index seek, serial, 3 sorts |

**The elapsed figure is the least interesting one, and it is the one most people
would quote.** The un-indexed plan spent 847 ms of processor time to deliver
137 ms of wall clock — it was parallelising its way around a full-table scan,
burning six threads' worth of work to hide the cost behind a shorter wait. The
indexed plan does the same job with 105 ms of CPU on one thread. On an idle
laptop that reads as a modest 15% improvement; under concurrency it is the
difference between a report costing one core-second and one costing eight.

A second finding, from measuring the single-client path separately: it reads
*exactly* as much as the unfiltered one — 6,879 and 1,768, identical to the
digit. Filtering to one client returns 105 rows instead of 5,775 and saves not a
single page, because the report ranks every client before narrowing to one. That
is a deliberate design decision, and this is its price.

Full method, per-table breakdown, the honest limits, and the committed execution
plans and raw `SET STATISTICS IO, TIME` captures: **[docs/performance.md](docs/performance.md)**.

Reproduce it yourself — the read counts will match exactly, the milliseconds will
not:

```powershell
dotnet run --project src/LexTime.Api measure
```

## Testing

xUnit against real SQL Server 2022 via Testcontainers. No in-memory provider,
no SQLite, no mocked `DbContext` — a test that cannot run against the real
engine is not testing what this repository claims to be good at.

Coverage is deliberate rather than uniform. Today, 131 tests cover the storage
constraints, the health contract, the access boundary, the maintenance verbs and
the seed generator — asserted against the database directly, so that
application-layer validation cannot mask a missing constraint. Two of them assert
that something is **absent**: a three-year-old billing date must be accepted,
because the 90-day backdating rule governs submissions and not stored history,
and adding the "obviously missing" date constraint would break the seed silently.

The generator's tests run at a hundredth of production scale with **no database
at all**, because it is a pure function of its options. That is where a
regression would actually be introduced, and it is what makes asserting
determinism — two runs producing identical rows — cheap enough to do on every
build rather than never.

Every domain rule gets a rejecting and an accepting case, and the rollup is asserted
against a **hand-computed fixture** — expected
cumulative totals, `LAG` deltas and ranks worked out by a human, not by running
the procedure and recording its output. Trivial CRUD gets one happy path and one
404. No coverage percentage is targeted or reported.

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
| One report, not several | The pattern is proven once; repeating it is padding |
| No secret management beyond `appsettings.Development.json` | Demo scope, said out loud rather than hidden |
| Clean-architecture layering at this size | Seventeen endpoints, including ten party routes, do not need a handler class each. The layering is here because it is worth showing done properly — a presentation decision, not one the problem forced |
| Static dashboard export committed under `wwwroot` | Keeps the reviewer quickstart at Docker plus .NET; UI contributors regenerate it explicitly with Node |

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

**5. It fell into the same trap twice, a day apart.** Mistake 2 was a stale
incremental build reporting a false pass. During implementation the agent ran
`dotnet ef migrations script --no-build` to review the generated SQL by hand, as
the constitution requires before committing a migration. The script contained
only the migrations-history table — no tables, no check constraint — because
`--no-build` had used an assembly compiled before the migration existed. Caught
by reading the output rather than skimming it. Both failures are the same shape:
a stale artefact reporting success.

**6. A test failed on its own fixture, not on the code.** The helper minting an
expired JWT set the expiry an hour in the past and left `notBefore` five minutes
in the past, so `notBefore > expires` and the token could not be constructed.
The test failed for a real reason that had nothing to do with the boundary it
was meant to exercise. Worth recording because a red test is not automatically
evidence about the thing under test — had the assertion been on the exception
type, it would have "passed" for entirely the wrong reason.

**7. A persisted timestamp was returned before database precision had been applied.**
The first client endpoint test compared the creation response with a subsequent read and
found equal-looking timestamps that differed below `datetime2(3)` precision. The stores now
reload newly inserted clients and matters before projecting them, and the test catches a
future regression instead of accepting a response that does not equal stored state.

The full log also records the misleading build error where a `--` inside
an XML comment broke `Directory.Build.props` and surfaced as
`NuGet.targets: Invalid framework identifier ''`. See `docs/agent-log.md`.

## Layout

```
├─ db/programmability/     usp_WeeklyBillableRollup.sql
├─ docs/                   prd.md, performance.md, agent-log.md, graphify/
├─ scripts/                Initialize-LocalDb.ps1
├─ src/                    Api, Application, Domain, Infrastructure
├─ tests/                  LexTime.IntegrationTests
├─ web/                    Next.js source; build syncs its export into Api/wwwroot
└─ .specify/               constitution, specs, plans, tasks
```

## Licence

MIT — see [LICENSE](LICENSE).
