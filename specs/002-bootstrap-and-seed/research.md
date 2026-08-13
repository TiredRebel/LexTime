# Phase 0 Research: Bootstrap and Seed

**Feature**: 002-bootstrap-and-seed | **Date**: 2026-08-13

R1 to R5 were made during the original combined planning run, before the P3 split and
before any code existed. This pass revalidates each against what feature 001 actually
shipped. Four survive unchanged. One assumption they all rested on did not, and it is
recorded first because it changes the quickstart.

---

## R0. Migrations are applied by the application, not by `dotnet ef`

**New in this pass.** The earlier plan assumed the script would shell out to
`dotnet ef database update`, because that is how a developer applies migrations by hand.

**Decision**: the script does not use `dotnet ef`. `LexTime.Api` gains a command-line
branch that resolves `LexTimeDbContext` from the built host and calls `MigrateAsync()`,
then exits with a status code. The script invokes that.

**Rationale**: `dotnet ef` is a **global tool that must be installed separately**. Feature
001's README already documents `dotnet-ef` as a prerequisite, which is a step beyond
"Docker and the .NET SDK" — and constitution P18 states the quickstart works from cold on
a machine with only those two. Every path that shells out to `dotnet ef` keeps that third
prerequisite, and the criterion in `docs/prd.md` §6.3 is not approximately satisfied, it is
satisfied or not.

Applying migrations in-process also removes a second problem the earlier plan had not
noticed: `dotnet ef` needs the `Microsoft.EntityFrameworkCore.Design` package on the
startup project and builds the project itself, so the script would depend on build state
as well as tool state.

**Alternatives considered**:

- *`dotnet ef database update` from the script.* Keeps the tool prerequisite. Rejected on
  P18.
- *`dotnet tool restore` with a tool manifest.* Removes the manual install but adds a
  restore step and still requires network access on first run. Better than the status quo,
  worse than not needing the tool.
- *Generating an idempotent SQL script at build time and running it.* Works without the
  tool at run time, but the script has to be regenerated whenever a migration is added and
  will silently go stale — the same class of failure as the two stale-artefact incidents
  already in `docs/agent-log.md`.

**Consequence**: feature 001's README prerequisite line ("no tool to install beyond
`dotnet-ef`") is removed by this feature, and the quickstart drops to two commands with
Docker and the SDK as the only prerequisites.

---

## R1. Where the seeder lives without a fifth project

**Carried forward. Validated — with one detail the original could not have known.**

**Decision**: seeding logic is a class in `LexTime.Infrastructure`. `LexTime.Api`'s entry
point inspects its arguments and, when invoked with a maintenance verb, runs the requested
operation against the configured connection and exits without starting the web host.

**Validation against shipped code**: `Program.cs` is top-level statements ending in
`await app.RunAsync()`, with `public partial class Program` appended so
`WebApplicationFactory` can host it. A verb branch must sit **after `builder.Build()` and
before `RunAsync()`**, and must not disturb the no-argument path — the twenty-one existing
tests all host the application with no arguments and would fail loudly if it did.

**Rationale**: unchanged. P4 caps the solution at four projects plus tests; seeding writes
to the database, so `Infrastructure` is where it belongs; the host already has
configuration binding, DI and connection resolution wired up.

**Alternatives considered**: a `LexTime.SeedTool` console project (a fifth project, a P4
violation); a seed endpoint (an eighteenth endpoint, and a destructive one); an
`IHostedService` (couples data generation to serving, and gives the script no exit code).

**New gotcha, learned the hard way in feature 001**: `dotnet run` honours
`Properties/launchSettings.json` and will force the Development environment regardless of
`ASPNETCORE_ENVIRONMENT`. During feature 001's security review this made a Production
fail-closed check appear to succeed when it had not run in Production at all. The script
must pass **`--no-launch-profile`** and set the environment explicitly, or it will be
testing something other than what it claims. Recorded in `docs/agent-log.md`.

---

## R2. Deterministic generation

**Carried forward. Validated, and the reference date is now pinned.**

**Decision**: one fixed integer seed drives a single pseudo-random generator; all dates are
offsets from a fixed reference date declared as a constant. Both are committed. Nothing in
the generation path calls `DateTime.Now`, `DateTime.UtcNow`, `Random.Shared`, `Guid.NewGuid`
or any other ambient source.

**Reference date: 2026-08-13.** Not chosen freely — feature 001 shipped
`WorkDateConstraintTests.AcceptsWorkDateAtTheOldestSeededBoundary`, which asserts that
`2024-08-13` is accepted as "the far edge of the range feature 002 seeds". A 24-month
window back from 2026-08-13 lands exactly there. Choosing any other anchor makes that test
assert something that is no longer true, which is worse than a failing test: it is a test
that still passes and no longer means anything.

**Rationale**: FR-020, FR-021 and SC-006 require row-for-row reproducibility, and P8
requires feature 003's index measurement to be comparable between runs. A rolling window
would also drift the dataset out from under any committed execution plan.

**Alternatives considered**: seeding from the current date (fails SC-006, and invalidates
any published number the moment a day passes); committing a generated data file
(reproducible, but adds tens of megabytes of near-binary content to a repository whose
point is to be read).

**Consequence**: `docs/performance.md` in feature 003 must cite the reference date beside
its measurements, or the numbers are not reproducible by a reader.

---

## R3. Waiting for database readiness

**Carried forward. Validated, and narrowed.**

**Decision**: the script polls by attempting an actual query until it succeeds or a
deadline passes, then fails with a message naming the timeout and how long it waited.

**Validation against shipped code**: `docker-compose.yml` already defines a container
health check that runs `SELECT 1` through `sqlcmd`, with a 20-second start period. The
script can read `docker compose ps` for that signal, but it still needs its own poll — the
container may have been started by hand, or by a different compose invocation, and the
script must work either way.

**Rationale**: FR-009. Container start and database readiness are different events, and a
fixed sleep is either too short on a cold machine or wasted time on a warm one. The failure
it produces — a connection error several layers deep — names nothing.

**Alternatives considered**: a fixed sleep (the failure mode the spec's edge case exists to
prevent); relying solely on the compose health check (does not cover a hand-started
container).

---

## R4. Applying stored procedures without an EF migration

**Carried forward. Validated, and the CA2100 consequence is now sharper.**

**Decision**: the script enumerates `db/programmability/*.sql` in sorted order and executes
each file's full contents through one command per file. Each file is authored
`CREATE OR ALTER PROCEDURE`, so re-application never requires a drop. An empty directory
logs "no procedures to apply" and continues.

**Validation against shipped code**: `db/programmability/` exists and contains only
`.gitkeep`, so the empty-directory path is the one that will actually run in this feature.
It is the default case, not an edge case, and should be tested as such.

**Rationale**: P7 verbatim, and FR-010. Keeping procedures out of migrations means their
history is a readable diff of the procedure rather than a sequence of opaque `Sql("...")`
calls.

**Security consequence, now firmer**: feature 001's `.editorconfig` sets
**`dotnet_diagnostic.CA2100.severity = error`**, not warning. Executing file contents makes
`CommandText` non-literal, so this will **fail the build** until suppressed at that single
call site with a justification naming the input as source-controlled. That suppression is a
P24 manual review item, and it is the second one in the repository — the first is in
`tests/.../DirectSql.cs` and is already reviewed and recorded.

**Alternatives considered**: `migrationBuilder.Sql()` (forbidden by P7); a dedicated
schema-migration tool for the procedure layer (a dependency and a second migration concept,
for one procedure).

---

## R5. Where the development token is minted

**Carried forward. Validated, and the claim shape is now determined by shipped code.**

**Decision**: minting happens inside the API process, invoked through the same
command-line surface as seeding, using the configured signing key. The token is printed to
standard output at the end of a successful run and never written into the repository.

**Validation against shipped code**: `AuthenticationSetup` already fixes everything the
minter must agree with — `SectionName` is `Jwt`, `SigningAlgorithm` is HMAC-SHA256,
issuer and audience are validated, `ClockSkew` is zero, and the key must be at least 32
bytes. The minter references those constants rather than restating them, so the printed
token cannot drift out of agreement with the validator. `ClockSkew = TimeSpan.Zero` in
particular means the printed expiry is the real expiry, with no five-minute grace.

**Rationale**: FR-024 and FR-025. One place knows the key and the claim shape.

**Alternatives considered**: minting in PowerShell (duplicates signing and claim shape in a
second language, which will diverge); a committed static token (fails FR-025 and puts a
credential-shaped string in source control).

**Open detail for `/speckit-tasks`**: the claim set. Feature 001's tests mint a token
carrying only `NameIdentifier`, and the fallback policy requires nothing more than an
authenticated user. The printed token should carry the same minimum, so that it does not
imply an authorisation model that does not exist.

---

## R6. Detecting "already seeded" versus "partially seeded"

**New in this pass.** FR-003, FR-004 and the spec's edge case require the script to skip a
complete environment, and to refuse rather than proceed on a partial one. The carried
research listed this as open; it is resolved here.

**Decision**: completeness is judged by row counts against the expected volumes, per table,
not by the presence of any rows. Three states:

- **Empty** — all four tables have zero rows. Seed.
- **Complete** — every table's count matches its expected volume. Skip and report.
- **Partial** — anything else. Exit non-zero telling the caller to re-run with `-Reset`.

**Rationale**: a seed interrupted midway leaves a plausible-looking database. Judging on
"are there any rows" reports it complete and hands the reviewer a dataset whose totals are
wrong in ways the rollup would faithfully report. The failure has to be loud.

**Alternatives considered**: a marker table or a row in a settings table recording
completion (more precise, but it is state about state, and a crash between the load and the
marker write reintroduces exactly the ambiguity it was meant to remove); checking only
`TimeEntries` (misses a failure that loaded entries but not matters — impossible given
foreign keys, but the check costs one query).

---

## R7. Verifying the seed without re-running it

**New in this pass.** FR-023 requires the script to verify distribution after seeding and
to exit non-zero if a band is missed.

**Decision**: verification is a set of aggregate queries run against the seeded database,
executed by the same in-process maintenance verb and reported line by line. The bands come
from SC-004 and SC-007 and are declared as constants beside the queries.

**Rationale**: the check has to run against the real dataset at full volume, because that is
the artefact whose properties matter and the only place a developer needs the answer. It is
cheap — aggregates over 400,000 rows, not a regeneration.

This complements rather than replaces the tests. The generator's determinism and shape are
tested at 1/100 scale with no database, which is where a regression would actually be
introduced; the script's verification catches a load that went wrong on the way in.

---

## Unknowns remaining

None. No `NEEDS CLARIFICATION` markers were carried into this plan. The five clarifications
from the `/speckit-clarify` session are recorded in the spec and none were reopened by this
revalidation.
