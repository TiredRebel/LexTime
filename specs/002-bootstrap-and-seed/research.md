# Phase 0 Research: Bootstrap and Seed

**Feature**: 002-bootstrap-and-seed | **Date**: 2026-08-12

**Status**: Carried forward from the original `001-local-environment-schema` planning run,
which covered both halves before the P3 split. These decisions were made against the same
constitution version and the same PRD sections and are recorded here so the work is not
lost. `/speckit-plan` for this feature should validate them rather than re-derive them,
and will produce `plan.md`, `data-model.md` and `quickstart.md`.

The schema-side decisions moved with their scope to
[feature 001](../001-solution-and-schema/research.md).

---

## R1. Where the seeder lives without a fifth project

**Decision**: Seeding logic is a class in `LexTime.Infrastructure`. `LexTime.Api`'s entry
point inspects its command-line arguments and, when invoked with the seed argument, runs
the seeder against the configured connection and exits without building the web host. The
bootstrap script calls `dotnet run --project src/LexTime.Api -- <seed argument>`.

**Rationale**: Constitution P4 caps the solution at four projects plus tests. Seeding
writes to the database, so `Infrastructure` is where it belongs under P5's boundary rule.
Something has to invoke it from a script, and the API host already has configuration
binding, dependency injection and connection-string resolution wired up — reusing that is
strictly less code than any alternative.

**Alternatives considered**:

- *A `LexTime.SeedTool` console project.* The conventional answer, and a direct P4
  violation. Rejected on the MUST.
- *A seed endpoint on the API.* Adds an eighteenth endpoint to a documented seventeen, and
  a destructive one behind the auth boundary. Rejected.
- *An `IHostedService` seeding on startup behind an environment variable.* Works, but
  couples data generation to serving and makes "did it finish?" ambiguous — the script
  needs a process that exits with a status code.
- *Pure T-SQL seeding in a `.sql` file.* No bulk-copy path, so the sub-minute target
  (FR-022) is unreachable at 400,000 rows, and the distribution logic would be far harder
  to read in T-SQL than in C#.

---

## R2. Deterministic generation

**Decision**: A single fixed integer seed drives one pseudo-random generator, and all
dates are computed as offsets from a fixed reference date declared as a constant. Both
values are committed. No call to any machine-entropy or current-time source occurs
anywhere in the generation path.

**Rationale**: FR-020, FR-021 and SC-006 require row-for-row reproducibility, and
constitution P8 requires the index before/after measurement in feature 003 to be
comparable — which it cannot be if the underlying dataset differs between runs. A rolling
"24 months back from today" window would also drift the dataset out from under any
committed execution plan.

**Alternatives considered**:

- *Seed from the current date.* Fails SC-006 and silently invalidates any committed
  performance number the moment a day passes.
- *Commit a generated `.csv` or `.bacpac` instead of generating.* Reproducible, but adds
  tens of megabytes of near-binary data to a repository whose point is to be read.

**Consequence**: the reference date is a published constant, and `docs/performance.md` in
feature 003 must cite it alongside its measurements, or the numbers are not reproducible
by a reader.

---

## R3. Waiting for database readiness

**Decision**: The script polls with a bounded retry loop, attempting an actual query
against the target server until it succeeds or a deadline passes, then fails with a message
naming the timeout and how long it waited.

**Rationale**: FR-009. Container start and database readiness are separate events, and SQL
Server's first start does non-trivial work before accepting connections. A fixed sleep is
either too short on a cold machine or wasted time on a warm one, and it produces the worst
possible failure — an error deep inside a connection attempt that names nothing.

**Alternatives considered**:

- *A fixed sleep.* Rejected; it is the failure mode the edge case exists to prevent.
- *A container-level health check with a dependency condition in Compose.* Worth having in
  addition, but the script must still handle a container started by hand, so the polling
  loop is required regardless.

---

## R4. Applying stored procedures without an EF migration

**Decision**: The script enumerates `db/programmability/*.sql` in sorted order and executes
each file's full contents through one command per file. Every file is authored
`CREATE OR ALTER PROCEDURE`, so re-application never requires a drop. An empty directory
logs "no procedures to apply" and continues.

**Rationale**: Constitution P7, and FR-010. Keeping procedures out of migrations means
their history is a readable diff of the procedure itself rather than a sequence of opaque
`Sql("...")` calls in migration files.

**Alternatives considered**:

- *`migrationBuilder.Sql()` in a migration.* Explicitly forbidden by P7.
- *A dedicated schema-migration tool for the procedure layer.* Solves a problem this
  repository does not have — one procedure — at the cost of a dependency and a second
  migration concept.

**Security consequence (P23/P24)**: the command text comes from a file rather than a
literal, which raises **CA2100** under `AnalysisModeSecurity=All`. The suppression is
applied at that single call site with a justification naming the input as
source-controlled, and it is recorded as a manual review item under P24 rather than treated
as analyzer noise.

---

## R5. Where the development token is minted

**Decision**: The bootstrap script mints the token by invoking the API host with a
token-printing argument, using the same signing key the API validates against. The token
is printed to the console at the end of a successful run and never written to a file in the
repository.

**Rationale**: FR-024 and FR-025. Minting it inside the API process means exactly one place
knows the key and the claim shape, so the printed token cannot drift out of agreement with
the validator. Reusing the argument mechanism from R1 means no new entry point.

**Alternatives considered**:

- *Minting in PowerShell.* Duplicates the signing logic and claim shape in a second
  language, which will silently diverge the first time either changes.
- *A committed static token.* Fails FR-025, and puts a credential-shaped string in source
  control that a scanner will flag and a reviewer will judge.

---

## Open for this feature's `/speckit-plan`

- How the script detects "already seeded" for the skip path in FR-003 and FR-004, and how
  it distinguishes that from the interrupted-partial-seed state that must demand a reset.
- Whether post-seed verification (FR-023) runs as SQL from the script or as a mode of the
  same API entry point used for seeding.
