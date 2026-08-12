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

Deferred items:
  - TODO(PRD_LOCATION): P2 and P19 bind this constitution to docs/prd.md, which is not yet
    committed to this repository. Land the PRD at that exact path before the first /plan run,
    or the Constitution Check has nothing to check P2 against.
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

**P3. Fits in three evenings. (MUST)**
Any single spec whose plan exceeds roughly one evening of implementation is
too big and must be split or trimmed before `/tasks` runs.

### II. Architecture

**P4. Three projects. No more. (MUST)**
`LexTime.Api`, `LexTime.Domain`, `LexTime.Infrastructure`, plus one test
project. No `Application` layer, no MediatR, no CQRS split, no generic
repository over `DbSet<T>`, no AutoMapper. Abstractions are introduced only
when a second concrete implementation actually exists in the repository.
*Rationale: ceremony disproportionate to a 400-line domain reads as
inexperience, not rigour.*

**P5. Right tool per access path. (MUST)**
EF Core owns writes and simple entity reads. Reporting reads go through stored
procedures invoked directly with `SqlCommand`/`SqlDataReader` — never through
`FromSqlRaw`, never mapped onto EF entities. The boundary between the two lives
in `LexTime.Infrastructure` and is explicit in the code, not incidental.
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
for wording. `Last amended` is updated on every change.

**Compliance review.** Before the repository is declared done, re-read this
document against the finished code once, end to end. Anything that drifted is
either fixed or the principle is honestly amended — never left silently
violated.

**Version**: 1.0.0 | **Ratified**: 2026-08-12 | **Last Amended**: 2026-08-12
