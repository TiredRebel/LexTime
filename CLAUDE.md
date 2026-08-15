# CLAUDE.md — working in this repository

Operating guide for an AI agent working on LexTime. It does not restate the rules; it points at
the two documents that hold them and records what this repository has actually cost to get wrong.

**Read these first, in this order:**

| Document | What it governs |
| --- | --- |
| `.specify/memory/constitution.md` | **Process.** 25 principles, P1–P25. `MUST` principles are gates, not preferences |
| `docs/prd.md` | **Scope.** §2.2 is the binding list of what is deliberately not built |
| `docs/agent-log.md` | **What went wrong before.** 25 entries. Most of the section below is distilled from it |

The constitution outranks the PRD on process and defers to it on scope. Where a convenient
default conflicts with a `MUST`, the constitution wins — including when the convenient default is
one of your own habits.

---

## What this is

A minimal timekeeping API for legal billing: .NET 9, SQL Server 2022, and one stored procedure
doing the interesting work. It is a **portfolio artifact, not a product** — every decision is
judged by whether it makes a senior .NET reviewer more confident in ten minutes of reading (P1).

Four projects plus tests, dependencies pointing inward and enforced by project references:

```
Api            → Application          Api → Infrastructure (composition only)
Application    → Domain               Infrastructure → Domain, Application
```

Those five references are the whole of P4's dependency rule and the list is exhaustive. **No
endpoint may name an `Infrastructure` type**; three files are permitted to and are enumerated in
P4 itself.

---

## The workflow

Every feature goes through Spec Kit in order, on its own branch (P22):

```
/speckit-specify → /speckit-clarify → /speckit-plan → /speckit-tasks → /speckit-implement
```

- **No implementation before its spec and plan exist** (P14). Not "mostly" — at all.
- Spec, plan and implementation land in **separate commits** on the feature branch (P17), and the
  branch reaches `main` as a single merge.
- Governance commits — constitution amendments, PRD edits, this file — may go on `main` directly.
- `/speckit-plan` **halts** if the design violates a `MUST`. The response is to change the design.
  Recording a violation in Complexity Tracking is only valid for `SHOULD` principles.
- If a spec will not fit in roughly one evening (P3), **split it before `/tasks` runs**. This has
  happened once already: feature 001 was split into 001 and 002 after the Constitution Check
  found it over the cap, and retrofitting the split was more expensive than making it early.

---

## Commands

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1        # container, schema, procedures, 400k seed, token
dotnet run --project src/LexTime.Api          # serve
```

That quickstart is **two commands and must stay two** (P18). Any step that exists but is
undocumented is a defect.

```powershell
dotnet build --warnaserror --no-incremental   # the gate
dotnet test                                   # 126 tests, real SQL Server via Testcontainers
dotnet run --project src/LexTime.Api state    # what condition is the database in
dotnet run --project src/LexTime.Api measure  # regenerate docs/performance.md's evidence
```

**Always `--no-incremental` when a build result is evidence.** See the first trap below.

---

## Traps this repository has already fallen into

Each of these cost real time. They are listed because they are the ones that produce a *confident
wrong answer* rather than an error.

### Builds and tooling

- **An incremental build reports success it has not earned.** Three times: a `--warnaserror` pass
  on an unchanged assembly (log #7), a `dotnet ef` script generated from a stale build (#2), and a
  file restored by `Copy-Item` whose *older* timestamp made MSBuild skip the rebuild entirely, so
  14 tests kept failing against a binary that no longer matched its source (#23). **If a build or
  test result is being used as evidence, pass `--no-incremental`.**
- **PowerShell working directory does not persist between tool calls.** A `Set-Location` in one
  call is gone by the next, and a bare `git status` then reports on the wrong repository — which
  once "verified" a file written into a different repo entirely (#5). **Use absolute paths, and
  `git -C E:\LexTime`.**
- **`$ErrorActionPreference = 'Stop'` plus `2>&1` turns any stderr line from a native command into
  a terminating error.** This reported "Docker is not responding" while Docker was serving
  containers (#13). `scripts/Initialize-LocalDb.ps1` has an `Invoke-Native` helper for this.
- **`dotnet run` honours `launchSettings.json`** unless `--no-launch-profile` is passed — which
  also drops `ASPNETCORE_ENVIRONMENT=Development` and therefore the connection string.
- **A `-replace` replacement string treats `$_` as the entire input.** It once inlined a whole
  script into itself (#14).

### SQL and the database

- **Procedure files under `db/programmability/` are executed as one `SqlCommand`.** No `GO`, and
  nothing before the `CREATE OR ALTER` — it must be first in its batch. `SET NOCOUNT ON` goes
  inside the body.
- **`DENSE_RANK`, `RANK`, `ROW_NUMBER` and `NTILE` return `bigint`.** Reading one into an `int`
  fails on the first row and the stack names only the mapping loop (#16).
- **`STATISTICS IO`/`TIME` output arrives after the final result set.** A reader disposed while
  anything is pending discards it, and the measurement then reports **zero logical reads with
  nothing failing** (#19). Drain with `NextResultAsync` before disposing.
- **Never use `DATEPART(weekday, …)`** — it moves with `SET DATEFIRST`. Week identity is a
  day-count ordinal anchored on 1900-01-01, which is a Monday. `IsoWeek − 1` is wrong every
  January; the ordinal is not.
- **CA2100 is `error` repository-wide.** Passing SQL through a helper hides the literal and the
  analyzer correctly objects. The fix is to let it see the constant, not to suppress (#21).
  **Four `CA2100` suppressions exist** — three in `Infrastructure` (`ProcedureApplier`,
  `BulkSeeder`, `SeedVerifier`), each reviewed under P24 and justified in `docs/agent-log.md`, and
  one in the test helper `DirectSql`. There is also a single `CA5394` on `SeedDataGenerator`,
  because determinism is the requirement and a cryptographic generator cannot be seeded to repeat.
  A fifth needs the same standard: scoped to the exact lines, with the reasoning written down.

### Tests

- **Pick boundary values that isolate the rule you are testing.** Rule 2's boundary is `1446`, not
  `1441` — rule 1 refuses `1441` first for a different reason. The same trap was written into a
  contract and then walked into one row further down (#22).
- **Tests about "today" must not use literal dates.** They pass now and fail in three months, and
  a suite that rots on a date fails while nothing is wrong. Inject `TimeProvider`; `FixedClock` is
  in the test project.
- **The accepting half of a pair is not decoration.** A rule proved only to refuse could be
  refusing everything. §6.4 requires both halves, and it was the accepting test that caught #22.
- **A green run does not discharge P15.** Generated SQL and generated expected values are read
  line by line. Two real findings came from reading code that was already passing (#17).

### Evidence and claims

- **No performance figure may be written before the run that produced it** (P8). And once
  published, it must keep tracing to committed evidence: a re-run silently overwrote the raw
  captures and the figures in the document stopped matching the files beside them (#20).
- **State licence terms from a primary source, not memory.** MediatR and AutoMapper were designed
  in before their licensing was checked, and checking it reversed the decision (#4).

---

## Things that must not change without an amendment

These are load-bearing. Changing one silently invalidates work already committed.

| Fixed | Why |
| --- | --- |
| Seed volumes, reference date `2026-08-13`, random seed `20260813` | Feature 004's performance evidence is measured against them, and its whole claim is that a reviewer regenerates the same numbers |
| `docs/performance.md` figures | Every one traces to a committed raw capture. Re-running the measurement means re-writing the document from the new files |
| Feature 003's hand-computed rollup expectations | Derived by hand, not captured from output (P12). An expectation edited to agree with the code has been made useless |
| The duration `CHECK` constraint and the two uniqueness indexes | The application also checks them; that duplication is deliberate (P6). Tests assert the database still refuses violations written outside the app |
| The six domain rules, expressed only in `TimeEntryRuleSet` | A rule restated in a handler is a defect even when it agrees — the two will eventually stop agreeing and nothing will say so |

If a change to any of these seems necessary, it is a PRD or constitution amendment in a separate
visible commit (P2, Governance) — not a quiet edit inside a feature.

---

## Documentation duties

- **Log what goes wrong** in `docs/agent-log.md`: what was generated, the symptom, how it was
  caught (P16). A repository claiming AI-assisted development with zero friction is not credible,
  and the log is the most-read evidence that it was real. If a feature genuinely hit nothing, say
  so rather than inventing it.
- **Security review before any commit touching auth or SQL** (P24), recorded in the same file.
  Six exist.
- **Everything carries XML doc comments** — types, methods, parameters, returns, exceptions,
  private members and test methods (P25). `GenerateDocumentationFile` makes CS1591 a build failure
  for public surface; below it, it is a review item. **A comment restating its signature is a
  defect, not compliance.** Say why the member exists, what the caller is responsible for, what
  the units are, or what happens at the boundaries.
- **State trade-offs** rather than hiding them (P19). Unstated shortcuts read as oversights;
  stated ones read as judgement.

---

## Known open items

Tracked here so they are not rediscovered. All are documentation or CI; no application code
remains in `docs/prd.md` §6.

- `azure-pipelines.yml` — restore → build → test → publish, `ubuntu-latest`, `--warnaserror` (§6.7–8)
- `docs/graphify/` — dependency graph and screenshot (§6.11)
- LLM Wiki pages ×4 (§6.12)
- **The seed exceeds rule 3 on 8,727 user-days**, the worst at 3,834 minutes. Not a defect in the
  rules — they bind at recording, not retroactively — but a realism defect in the seed that
  feature 005 made visible. Fixing it invalidates feature 004's measurement, so it needs a
  feature that says so (`docs/agent-log.md` #25).
