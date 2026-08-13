<!--
Sync Impact Report
==================
Version change: (template, unversioned) → 1.0.0
Rationale: initial ratification. The scaffold at .specify/memory/constitution.md was the
unmodified Spec Kit template with zero project-specific content, so this is an adoption,
not an amendment.

Modified principles: none (no prior principles existed)

Added sections:
  - Core Principles → I. Purpose and audience (P1–P3)
  - Core Principles → II. Architecture (P4–P7)
  - Core Principles → III. Data and performance (P8–P10)
  - Core Principles → IV. Testing (P11–P13)
  - Core Principles → V. Working with the agent (P14–P17)
  - Core Principles → VI. Documentation (P18–P20)
  - Governance (precedence, Constitution Check, amendment, compliance review)

Removed sections:
  - [SECTION_2_NAME] / [SECTION_3_NAME] template slots — left undefined rather than
    filled with invented content. Their intended subject matter (technology constraints,
    development workflow) is already covered by Articles II–VI.

Deferred items: none

--------------------------------------------------------------------------------

Version change: 1.0.0 → 1.1.0
Rationale: MINOR. Four principles added, none removed, none reversed.

Added principles:
  - P21 (Article II) Composition roots are extension methods
  - P22 (Article V)  Every feature starts on its own branch
  - P23 (Article VII) The quality gate is reproducible by the reviewer
  - P24 (Article VII) Security review precedes every commit touching auth or SQL

Added sections:
  - Core Principles → VII. Code quality and security

Modified principles: none. P4 was reviewed against a proposal to adopt full clean-
architecture layering and left unchanged; that proposal was later adopted in the
v2.0.0 entry below, minus its library dependencies.

Removed sections: none

Resolved from v1.0.0:
  - TODO(PRD_LOCATION) — docs/prd.md is now committed at the path P2 and P19 name.

Deferred items:
  - Directory.Build.props and .editorconfig are named by P23 but not yet created; they
    land with the solution scaffold in evening 1, per P14 (no implementation before spec).

--------------------------------------------------------------------------------

Version change: 1.1.0 → 2.0.0
Rationale: MAJOR. P4 is redefined and its former prohibitions are reversed.

Modified principles:
  - P4 "Three projects. No more." → "Four projects, layered, with dependencies
    pointing inward." Reversed: the ban on an Application layer is lifted, and the
    "abstractions only when a second implementation exists" clause is dropped —
    Application-declared interfaces with one Infrastructure implementation are now
    the expected shape. Use cases become handler classes and DTO mapping becomes
    ToDto() extension methods. The bans on MediatR, on AutoMapper and on a generic
    repository over DbSet<T> are NOT lifted and remain in force; the libraries were
    proposed with the layering and dropped before this amendment was committed
    (see PRD §2.2).
  - P5 clarified (no semantic change): the reporting handler depends on an interface;
    the SqlCommand implementation is the only place raw ADO.NET appears.
  - P21 extended: one DI registration extension method per layer.

Added principles:
  - P25 (Article VI) Everything is documented in XML doc comments, enforced by
    GenerateDocumentationFile + CS1591 + --warnaserror. Its consequence for PRD §2.2:
    the StyleCop row's stated reason was inverted — mandatory doc comments are now
    wanted, so StyleCop is rejected as redundant to CS1591 rather than as noise.

Removed principles: none

Consequences accepted with this amendment:
  - P3 is the principle most strained, and was retitled "Fits in three evenings" →
    "One evening per spec" in this amendment. Its rule is unchanged — it always
    capped a single spec, never the project — but the old title contradicted the
    PRD §7 budget once that moved to four evenings. If the scaffold overruns, P3
    governs and scope is cut, not P4.
  - No new third-party runtime dependency is introduced by this amendment. MediatR
    and AutoMapper were part of the original proposal and were dropped once their
    licensing was checked: both moved to a paid licence above a revenue threshold
    (MediatR from v13.0.0, AutoMapper from v15.0.0) and MediatR v13+ requires a
    registered licence key at runtime, which would have added a signup step to the
    P18 quickstart. Recorded in PRD §2.2.

--------------------------------------------------------------------------------

Version change: 2.0.0 → 2.0.1
Rationale: PATCH. A contradiction inside P4 is removed. Nothing a design may or must do
changes.

Modified principles:
  - P4: the dependency rule is restated as five enumerated project references, up from a
    prose list of two chains, and the gate is stated as exhaustive — a reference not on the
    list fails it. Two edges are added, both of which the repository has always had:

      * `Infrastructure` → `Application`. The old text listed `Api` → `Application` →
        `Domain` and `Infrastructure` → `Domain`, then required in the same paragraph that
        "where Application needs infrastructure it declares the interface and Infrastructure
        implements it". Those cannot both hold: an implementing type must see the interface
        it implements. The prose already mandated the edge; the list omitted it. Now scoped
        explicitly — `Infrastructure` may reference `Application` to implement interfaces
        declared there, and for nothing else.

      * `Api` → `Infrastructure`. Present since feature 001 and never listed, because
        `Program.cs` cannot call `AddLexTimeInfrastructure()` without it. Now named, with
        the rule that actually carries the meaning stated alongside it: no *endpoint* may
        name an `Infrastructure` type. The three files that legitimately do — composition,
        the health probe, the maintenance verbs — are enumerated, so a fourth is a violation
        rather than a precedent.

    Enumerating the references rather than writing chains also avoids implying that
    `Infrastructure` reaches `Domain` only transitively. It does not — it references `Domain`
    directly, for the entity configurations.

    PATCH rather than MINOR: no new permission is granted and no rule is widened. Both
    added edges already existed in the solution and the second was already required by P21.
    What changes is that the list now says what the prose always meant.

Found by: feature 003, in two steps worth separating. The first edge surfaced from the
compiler — `SqlWeeklyBillableRollupReader` implements `IWeeklyBillableRollupReader`, which
lives in `LexTime.Application`, so the reference was unavoidable. The `/plan` Constitution
Check had already passed that design on P4 without flagging anything, because the prose was
read as governing and the arrow list as illustrative; `docs/agent-log.md` entry 18 records
that a gate checked by reading can be passed by reading it the convenient way.

The second edge surfaced only from mechanically diffing the amended list against every
`ProjectReference` in `src/`. `Api` → `Infrastructure` had been in the solution since
feature 001, unlisted, through three Constitution Checks that each read P4 and did not
notice. Reading the principle found one omission; comparing it against the build found the
other.

Added sections: none
Removed sections: none
Deferred items: none

--------------------------------------------------------------------------------

Amendment-clause note: v1.1.0 and v2.0.0 land in a single commit. v1.1.0 was written
but never committed before v2.0.0 was requested, so there is no v1.1.0 commit to
separate them into. Stated here rather than implying the "dedicated commit" rule was
satisfied for both.
  - PRD §2.2 previously listed this layering as out of scope. That row is removed, and
    §5's "three projects, not seven" paragraph is rewritten. P2 binds to the PRD as it
    now stands.
-->

# LexTime Constitution

**Project:** LexTime — minimal timekeeping API (interview demo repository)

This constitution governs every `/specify`, `/plan`, `/tasks`, `/implement` and
`/analyze` run in this repository. Principles marked **MUST** are gates: a plan
that violates one is rejected at the Constitution Check step and must be
reworked, not waived with a comment. Principles marked **SHOULD** are strong
defaults that may be departed from only with a one-line justification recorded
in the plan's Complexity Tracking table.

## Core Principles

### I. Purpose and audience

**P1. The artifact is a hiring signal, not a product. (MUST)**
Every decision is judged by: *does this make a senior .NET reviewer more
confident in 10 minutes of reading?* Work that does not move that needle is
out of scope regardless of how correct it is.

**P2. The PRD's out-of-scope list is binding. (MUST)**
`docs/prd.md` §2.2 enumerates what is deliberately not built. No spec, plan or
task may introduce any of it. If a feature seems necessary, the correct move is
to amend the PRD in a separate, visible commit — not to slip it into an
implementation task.

**P3. One evening per spec. (MUST)**
Any single spec whose plan exceeds roughly one evening of implementation is
too big and must be split or trimmed before `/tasks` runs. The cap is per spec,
not per project; the project's own budget lives in PRD §7 and is currently four
evenings. When the two collide — a spec that will not fit without abandoning a
design rule — the spec is cut, never the rule.

### II. Architecture

**P4. Four projects, layered, with dependencies pointing inward. (MUST)**
`LexTime.Api`, `LexTime.Application`, `LexTime.Domain`,
`LexTime.Infrastructure`, plus one test project. The dependency rule is the
gate, and these five project references are the whole of it — any reference not on this
list fails the gate:

```
Api            → Application
Api            → Infrastructure     (composition only, see below)
Application    → Domain
Infrastructure → Domain
Infrastructure → Application
```

`LexTime.Domain` references no other project and no persistence, HTTP or
mapping package. Where `Application` needs infrastructure it declares the
interface and `Infrastructure` implements it — an interface with a single
implementation is expected here rather than deferred. `Infrastructure` →
`Application` is what makes that possible and is the only reason it is
permitted: `Infrastructure` may reference `Application` to implement interfaces
declared there, and for nothing else. `Application` knows nothing of
`Infrastructure`, which is the direction the principle's title names.

`Api` → `Infrastructure` exists so that `Program.cs` can call
`AddLexTimeInfrastructure()` and bind those interfaces to their implementations. A
composition root has to see every layer it wires; that is what makes it the composition
root.

**No endpoint may name an `Infrastructure` type.** That is the rule this edge could
otherwise hide, and it is the one worth checking: an endpoint reaching past `Application`
for a `DbContext` or a reader defeats the layering while leaving the project references
looking correct. Three files in `LexTime.Api` reference `Infrastructure` and none of them is
an endpoint — they are named here so the list is a fixed set rather than a habit:

- `Program.cs` — composition.
- `HealthChecks/DatabaseHealthCheck.cs` — a liveness probe has to touch the database it is
  reporting on, and routing it through a use case would invent one.
- `Maintenance/MaintenanceCommands.cs` — the host's command-line surface, which runs instead
  of the web host and serves the bootstrap script. Not an HTTP endpoint and subject to no
  request pipeline.

A fourth site is a violation unless this list is amended to admit it.

Every use case is one handler class in `LexTime.Application`, registered in DI
and injected where it is used. Entity-to-DTO translation is a `ToDto()`
extension method next to the DTO it produces. No mediator library and no
mapping library: the endpoint is the only caller each handler will ever have,
and mapping six DTOs by hand is checked at compile time rather than at runtime.
An `Api` endpoint validates its input, invokes the handler, and maps the result
to a status code — it holds no business logic and touches no `DbContext`.
*Rationale: the layering is itself part of the signal this repository sends. A
reviewer looking for clean architecture should find it named and enforced by
project references, not inferred from a folder structure.*

**P5. Right tool per access path. (MUST)**
EF Core owns writes and simple entity reads. Reporting reads go through stored
procedures invoked directly with `SqlCommand`/`SqlDataReader` — never through
`FromSqlRaw`, never mapped onto EF entities. The boundary between the two lives
in `LexTime.Infrastructure` and is explicit in the code, not incidental: the
reporting handler in `LexTime.Application` depends on an interface, and the
`SqlCommand`/`SqlDataReader` implementation of that interface is the only place
raw ADO.NET appears.
*Rationale: this split is the technical argument the repository exists to make;
blurring it erases the point.*

**P6. Domain rules live in the domain, and also in the database. (MUST)**
The six rules in PRD §2.1 are enforced in C# with clear error messages, and the
ones expressible as `CHECK` constraints (increment, magnitude) are additionally
enforced in the schema. Duplication here is intentional defence in depth.

**P7. Stored procedures are source-controlled, idempotent, and versioned with the code. (MUST)**
Each lives in one file under `db/programmability/`, is written
`CREATE OR ALTER PROCEDURE`, is applied by `Initialize-LocalDb.ps1`, and is
never created by an EF migration.

**P21. Composition roots are extension methods. (MUST)**
Endpoint registration and service registration are exposed as extension methods
— `app.MapClientEndpoints()`, `services.AddLexTimeApplication()`,
`services.AddLexTimeInfrastructure()`, one registration method per layer — so
`Program.cs` reads as a table of contents rather than a two-hundred-line wall.
This is the only mandated use of extension methods; adding them elsewhere to
look idiomatic is churn and is not required by this principle.
*Rationale: `Program.cs` is the first file a reviewer opens; it should be
scannable in thirty seconds.*

### III. Data and performance

**P8. Performance claims are measured, never asserted. (MUST)**
Any statement in the README or wiki about speed, plan shape, or index benefit
must be backed by a captured `SET STATISTICS IO, TIME ON` run and an execution
plan committed to the repository. Placeholder numbers, illustrative numbers,
and numbers recalled from experience are all prohibited. If the measured
improvement turns out to be unimpressive, the unimpressive number is published
along with an honest explanation of why.
*Rationale: a reviewer who spots one invented figure discards the whole repo.*

**P9. Seed data is realistic in shape, not merely in volume. (SHOULD)**
Entries cluster on weekdays, distribute unevenly across clients (a few large,
a long tail of small), and include a non-billable minority. Uniformly random
data produces uniformly boring plans and a rollup nobody would believe.

**P10. The rollup is the headline. (MUST)**
`dbo.usp_WeeklyBillableRollup` gets more design attention, more tests and more
documentation than the other sixteen endpoints combined. It must demonstrate
`SUM() OVER (PARTITION BY ... ORDER BY ...)`, `LAG()` and `DENSE_RANK()`, be
readable, and be commented where the window frame is non-obvious.

### IV. Testing

**P11. Integration tests run against real SQL Server. (MUST)**
Testcontainers with the SQL Server 2022 image. No in-memory provider, no
SQLite, no mocked `DbContext`. A test that cannot run against the real engine
is not testing the thing this repository claims to be good at.

**P12. The rollup is tested against a hand-computed fixture. (MUST)**
A small deterministic dataset whose expected cumulative totals, `LAG` deltas
and ranks were worked out by a human, not by running the procedure and
recording its output. Empty ranges and zero-billable weeks are covered.
*Rationale: window-function bugs are silent and self-consistent; only an
independently derived expectation catches them.*

**P13. Coverage is deliberate, not uniform. (SHOULD)**
Concentrate on domain rules and the reporting path. Trivial CRUD gets one
happy-path and one 404 test each. No coverage-percentage target is set or
reported.

### V. Working with the agent

**P14. Spec before code. (MUST)**
No implementation task is started before its spec and plan exist in
`.specify/`. Specs describe behaviour and constraints; they do not contain
implementation code.

**P15. Generated SQL and generated tests are reviewed line by line. (MUST)**
Two categories of agent output are never accepted on the strength of a green
run: SQL involving window functions or joins, and tests whose expected values
the agent derived itself. Both are read manually before commit.
*Rationale: an agent that writes both the procedure and its expected output
will happily agree with itself.*

**P16. Agent mistakes are logged, not quietly fixed. (MUST)**
When the agent produces something wrong, record in `docs/agent-log.md`: what it
generated, what the symptom was, and how it was caught. At least three of these
entries reach the README. A repository that claims AI-assisted development and
shows zero friction is not credible.

**P17. Commit history shows the process. (SHOULD)**
Spec, plan and implementation land in separate commits so the spec-driven
workflow is legible to someone reading the log rather than only asserted in the
README.

**P22. Every feature starts on its own branch. (MUST)**
Before the first implementation task of a spec is started, create a branch — or
a `git worktree` — named for that spec. The separate spec, plan and
implementation commits required by P17 land on that branch and reach `main` as a
single merge. Governance commits — constitution amendments, PRD edits, README
and documentation fixes that belong to no spec — may be made on `main` directly.
*Rationale: P17 claims the history shows the process; one branch per spec is
what makes that claim legible instead of a flat list of commits.*

### VI. Documentation

**P18. The quickstart is two commands and it works from cold. (MUST)**
Verified on a machine with only Docker and the .NET 9 SDK. Any step that exists
but is undocumented is a defect.

**P19. Trade-offs are stated, not hidden. (MUST)**
Where a shortcut was taken — symmetric-key JWT, no multi-tenancy, hard deletes,
pipeline stopping at publish — the README names it and says why. Unstated
shortcuts read as oversights; stated ones read as judgement.

**P20. English is the repository language. (MUST)**
README, code comments, commit messages, specs and wiki pages, all in English.

**P25. Everything is documented in XML doc comments. (MUST)**
Every type, method, property, parameter, return value and thrown exception
carries an XML documentation comment: `<summary>` on the member, `<param>` on
each parameter, `<returns>` where something is returned, `<exception>` where one
is thrown by contract. Private and internal members are documented to the same
standard as public ones. Every project states its purpose in a `<Description>`
element in its `.csproj`, which is what "module level" means in a solution with
no modules.

Enforcement is the compiler, not a convention:
`<GenerateDocumentationFile>true</GenerateDocumentationFile>` in
`Directory.Build.props` turns on CS1591 for every undocumented publicly visible
member, and the pipeline's `--warnaserror` (P23) turns that into a build
failure. CS1591 only sees public surface; the same standard applies below it and
is a review item rather than a build item.

A comment that restates its signature is a defect, not compliance.
`/// <summary>Gets the client id.</summary>` on `ClientId` adds nothing and will
be rejected in review the same as a missing comment. Say why the member exists,
what the caller is responsible for, what the units are, or what happens at the
boundaries — the six-minute increment rule, the 90-day window, the rate
snapshot. Where there is genuinely nothing to add beyond the name, that is a
signal the member is well named and the summary should state its contract in one
short clause rather than padding.
*Rationale: the reviewer in P1 reads this repository without being able to ask
questions. Documentation is the only channel available, and filler comments
consume that channel without using it.*

### VII. Code quality and security

**P23. The quality gate is reproducible by the reviewer. (MUST)**
Code quality is enforced by the .NET SDK's built-in Roslyn analyzers, configured
in one root `Directory.Build.props` and one root `.editorconfig`. No commercial
tool and no third-party analyzer package. Any quality claim the README makes
must be reproducible by `dotnet build` on the reviewer's own machine. The
pipeline builds with `--warnaserror`; local builds do not, so a warning blocks
the merge without blocking the edit-run loop.
*Rationale: the same objection P8 raises about performance numbers — a result
the reviewer cannot regenerate is an assertion, not evidence.*

**P24. Security review precedes every commit touching auth or SQL. (MUST)**
`AnalysisModeSecurity=All` and `NuGetAudit` run on every build and cover the
mechanical cases: CA2100 for concatenated SQL, the CA5xxx cryptography rules,
and vulnerable package references. On top of that, no commit touching JWT
validation, connection strings, or SQL assembled by string concatenation is
made without a manual review pass. Findings are fixed, or recorded in
`docs/agent-log.md` together with the reason they were accepted. P15 governs
whether generated SQL is *correct*; this principle governs whether it is *safe*.
*Rationale: the shortcuts PRD §2.3 admits to — symmetric dev key, no secret
management — only read as judgement if someone actually looked.*

## Governance

**Precedence.** This constitution outranks the PRD on process questions and
defers to the PRD on scope questions. Where an agent instruction, a skill, or a
convenient default conflicts with a **MUST** here, this document wins.

**Constitution Check.** `/plan` halts and reports if the proposed design
violates any **MUST**. The correct response is to change the design. Recording
a violation in Complexity Tracking is only valid for **SHOULD** principles.

**Amendment.** Amendments are made by editing this file in a dedicated commit
that states the reason. Version increments follow semver: MAJOR for removing or
reversing a principle, MINOR for adding one or materially expanding one, PATCH
for wording. `Last amended` is updated on every change. Principle numbers are
permanent identifiers assigned in amendment order: a new principle takes the
next free number and is filed under the article it belongs to, so numbering runs
in order of age rather than in order of appearance. Numbers are never reused and
never renumbered, so a reference to P4 means the same thing in every commit.

**Compliance review.** Before the repository is declared done, re-read this
document against the finished code once, end to end. Anything that drifted is
either fixed or the principle is honestly amended — never left silently
violated.

**Version**: 2.0.1 | **Ratified**: 2026-08-12 | **Last Amended**: 2026-08-13
