# Feature Specification: Bootstrap and Seed

**Feature Branch**: `002-bootstrap-and-seed`

**Created**: 2026-08-12

**Status**: Draft

**Input**: Split from the original `001-local-environment-schema` after the
`/speckit-plan` Constitution Check found it exceeded the one-evening cap in P3. This half
covers the bootstrap script, deterministic data generation, post-seed verification and
development token minting. The solution structure, schema, container definition, access
boundary and health endpoint are feature 001.

## Clarifications

### Session 2026-08-12

Carried forward from the original spec; each of these was asked and answered about work
that now lives in this feature.

- Q: Should seeded historical entries be exempt from the 90-day backdating rule that will
  apply to entries created through the API? → A: Yes. The 90-day window is a period-close
  rule governing new submissions only; recorded history is not retroactively invalidated
  by it. Seeded data spans the full 24 months.
- Q: Should the seeded dataset include inactive clients, matters and timekeepers, and
  should historical time entries be allowed to reference them? → A: Yes. A minority
  (roughly 10–15%) of each are inactive, and their historical entries are retained.
  Deactivation is forward-looking only.
- Q: How should a reviewer obtain a token so they can call a protected route? → A: The
  bootstrap script prints a ready-to-use development token at the end of its run, signed
  with the same symmetric development key. No token endpoint is added.
- Q: When the reset option is used, how far should it tear down — just the database, or
  the container and its storage volume as well? → A: The database only. The container
  keeps running. A full wipe is the container tool's own down-with-volumes command,
  documented rather than reimplemented in the script.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reviewer runs the project from a cold machine (Priority: P1)

Someone evaluating this repository clones it onto a machine that has only container
tooling and the pinned SDK. They read the two commands in the README, run them, and end
up with a running service and a populated database without consulting anyone, reading
source, or discovering an undocumented step.

**Why this priority**: This is the repository's first impression and the binding
done-criterion (`docs/prd.md` §6.1–6.3). Constitution P18 makes any undocumented step a
defect. Feature 001 leaves a developer with three manual commands; this story reduces
them to two documented ones and adds the data.

**Independent Test**: On a machine with no prior project state, run the two documented
commands in order and request the health endpoint.

**Acceptance Scenarios**:

1. **Given** a machine with container tooling and the pinned SDK but no project state,
   **When** the reviewer runs the bootstrap command, **Then** it completes without error
   and reports which steps it performed.
2. **Given** the bootstrap has completed, **When** the reviewer starts the service and
   requests health, **Then** it responds successfully.
3. **Given** the bootstrap has completed, **When** the reviewer counts rows in each
   table, **Then** the volumes match the seeded figures in this spec.
4. **Given** the bootstrap has completed, **When** the reviewer uses the token it printed
   against a protected route, **Then** the request is accepted without any configuration
   change on their part.

---

### User Story 2 - A developer re-runs the bootstrap safely (Priority: P2)

Someone working on the project runs the bootstrap again — after a reboot, after pulling
changes, or because they are unsure whether it finished. It does not duplicate data, does
not fail because objects already exist, and does not silently leave the database in a
half-built state.

**Why this priority**: Directly required by `docs/prd.md` §6.1. An apply-once-only script
is a trap for the reviewer in User Story 1 as much as for the author, but the cold path
has to work before the repeat path matters.

**Independent Test**: Run the bootstrap twice in succession and compare row counts and
step reporting between the runs.

**Acceptance Scenarios**:

1. **Given** a fully bootstrapped environment, **When** the script runs a second time,
   **Then** it completes successfully and row counts are unchanged.
2. **Given** a fully bootstrapped environment, **When** the script runs a second time,
   **Then** each step reports whether it acted or skipped, distinguishably.
3. **Given** a running container with no schema applied, **When** the script runs,
   **Then** it applies the schema and seeds without recreating the container.
4. **Given** a request to rebuild, **When** the script runs with its reset option,
   **Then** the database is dropped, recreated, migrated and reseeded while the container
   continues running throughout.
5. **Given** the reset option is not supplied, **When** the script runs against a
   complete environment, **Then** no data is dropped under any circumstance.
6. **Given** stored procedure files exist, **When** the script runs, **Then** each is
   applied and re-application replaces rather than duplicates or errors.

---

### User Story 3 - The seeded data supports a believable report (Priority: P3)

The dataset is shaped like real timekeeping activity, not uniform random noise, so the
reporting feature built on it produces figures a reader would believe and query plans
that differ visibly with and without an index.

**Why this priority**: Constitution P9. Uniform data produces uniform plans and a rollup
nobody would credit, which undermines the two things this repository exists to show. It
is P3 because a wrongly-shaped seed can be corrected without redoing the script.

**Independent Test**: Query the seeded data for the distribution properties below and
confirm each falls within its stated band.

**Acceptance Scenarios**:

1. **Given** the seeded dataset, **When** entries are grouped by day of week, **Then**
   weekend entries are a small minority rather than two sevenths of the total.
2. **Given** the seeded dataset, **When** clients are ranked by total logged minutes,
   **Then** a few account for a disproportionate share and a long tail accounts for very
   little.
3. **Given** the seeded dataset, **When** entries are grouped by billable flag, **Then** a
   meaningful minority are non-billable.
4. **Given** the seeded dataset, **When** every duration is checked, **Then** all satisfy
   the increment and magnitude rules without exception.
5. **Given** two seed runs from the same inputs, **When** the datasets are compared,
   **Then** they are identical row for row.

---

### Edge Cases

- **Container tooling is not running.** The script must say so in one plain sentence and
  stop, rather than failing deep inside a connection timeout.
- **The database port is already in use.** The failure must name the port conflict.
- **The container is up but the database is not yet accepting connections.** The script
  waits for readiness on a bounded timer rather than sleeping a fixed interval.
- **No stored procedures exist yet.** `db/programmability/` is empty until feature 003. An
  empty directory is a normal state, not an error.
- **Seeding is interrupted partway.** A later run must either complete the environment or
  report that a reset is required. It must not leave a partial seed that looks complete.
- **The wrong SDK is the default on the machine.** The pinned version must govern, and the
  failure message must name the required version.
- **A time entry would be generated with a future date.** Dates are drawn relative to the
  fixed reference date; none may fall after it.

## Requirements *(mandatory)*

### Functional Requirements

**The script**

- **FR-001**: The environment MUST be brought up by a single command requiring no
  arguments for the default path.
- **FR-002**: That command MUST verify prerequisites, start the container, wait for
  readiness, apply migrations, apply stored procedures, seed, verify the seed and print a
  development token — in that order, reporting which steps it performed.
- **FR-003**: The command MUST be safe to run repeatedly. A second run on a complete
  environment MUST leave row counts unchanged and MUST NOT fail on existing objects.
- **FR-004**: Each step MUST report whether it acted or skipped, distinguishably. A script
  that reports success identically in both cases gives a developer no way to tell a
  working environment from a no-op.
- **FR-005**: The command MUST offer an explicit reset option that discards data and
  rebuilds. Reset MUST NOT be the default.
- **FR-006**: Reset MUST drop and recreate the database only, leaving the container
  running. It MUST NOT stop, remove or rebuild the container, and MUST NOT reimplement
  the container tooling's own teardown.
- **FR-007**: Reset MUST NOT prompt for confirmation — the explicit switch is the
  confirmation, and a prompt would make the script unusable unattended. The switch MUST
  be named so its destructive effect is obvious from the command line.
- **FR-008**: The README MUST document the container tooling's own down-with-volumes
  command as the way to discard the container and its storage.
- **FR-009**: The command MUST wait for the database to accept a query before applying
  the schema, and MUST fail with a clear message if readiness is not reached within a
  bounded time. A fixed sleep does not satisfy this.
- **FR-010**: Stored procedures MUST be applied from source files in a deterministic
  order, MUST be re-appliable without being dropped first, and MUST NOT be created by a
  migration. An empty procedure directory MUST be reported and MUST NOT be an error.
- **FR-011**: Every failure mode in Edge Cases MUST produce a message naming its cause
  before any stack trace, and a distinct non-zero exit code per class of failure.

**Seed data**

- **FR-012**: Seeding MUST produce approximately 25 timekeepers, 60 clients, 220 matters
  and 400,000 time entries spanning 24 months.
- **FR-013**: Seeded entries MUST concentrate on weekdays, with weekend activity a small
  minority rather than a uniform share.
- **FR-014**: Seeded activity MUST be unevenly distributed across clients — a few large
  clients and a long tail of small ones.
- **FR-015**: A meaningful minority of seeded entries MUST be non-billable.
- **FR-016**: Roughly 10–15% of seeded clients, matters and timekeepers MUST be inactive,
  so the rule requiring an active matter of an active client has a realistic fixture to be
  tested against rather than one fabricated per test.
- **FR-017**: Inactive clients, matters and timekeepers MUST retain their historical
  entries. Deactivation is forward-looking and MUST NOT remove, hide or invalidate what
  was recorded while they were active.
- **FR-018**: Every seeded entry MUST satisfy the duration rules — positive, a multiple of
  six minutes, not exceeding 1440 — and MUST NOT be dated after the reference date.
- **FR-019**: Seeded entries are EXEMPT from the 90-day backdating limit. That limit
  constrains what may be *submitted* through the API; it is not an invariant on recorded
  history. The seed spans the full 24 months and the vast majority of entries are older
  than 90 days by design.
- **FR-020**: Seeding MUST be reproducible: the same inputs MUST produce the same dataset
  row for row, so that performance measurements taken before and after an index change
  are comparable.
- **FR-021**: Dates MUST be computed as offsets from a fixed, committed reference date,
  not from the current date. A rolling window would drift the dataset out from under any
  committed measurement.
- **FR-022**: Seeding MUST complete in well under a minute once the database is running.
- **FR-023**: After seeding, the script MUST verify the distribution properties in FR-013
  to FR-018 and report each result. A distribution outside its band MUST be a non-zero
  exit.

**Development token**

- **FR-024**: The script MUST print a usable development token at the end of a successful
  run, and the README MUST show how to supply it when calling a protected route. No
  endpoint that issues tokens may be added.
- **FR-025**: The token MUST carry an expiry long enough to survive an evaluation session
  without reissue, MUST be valid only against the development signing key, and MUST NOT
  be written into the repository or committed.

**Documentation**

- **FR-026**: The quickstart in the README MUST consist of exactly two commands, with no
  undocumented prerequisite or manual step.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person with no prior knowledge of this project reaches a running service
  and a populated database using only the README, in under 10 minutes excluding the
  first-time image download, and without asking a question.
- **SC-002**: The bootstrap completes in under 3 minutes with the image present locally,
  with data generation accounting for under a minute of that.
- **SC-003**: Running the bootstrap twice produces identical row counts, and the second
  run reports skipped work rather than repeating it.
- **SC-004**: Weekend entries are under 10% of all entries; the ten busiest clients
  account for at least half of all logged minutes; non-billable entries are between 10%
  and 25% of the total.
- **SC-005**: 100% of seeded entries satisfy the duration increment and magnitude rules
  and carry a date no later than the reference date — zero exceptions across all 400,000
  rows. Entries older than 90 days are expected, not violations.
- **SC-006**: Two seed runs from the same inputs produce datasets identical row for row.
- **SC-007**: Between 10% and 15% of seeded clients, of matters and of timekeepers are
  inactive, and at least one inactive client, one inactive matter and one inactive
  timekeeper each have historical entries against them.
- **SC-008**: A reviewer can call a protected route successfully using only the token the
  script printed, without editing configuration or generating anything themselves.
- **SC-009**: Each documented failure mode produces a message naming its cause, as judged
  by someone who has not read the script.

## Assumptions

- **Idempotency means skip, not rebuild.** A second run detects a complete environment and
  leaves it alone, reporting what it skipped. Rebuilding is available behind the explicit
  reset option. Reseeding 400,000 rows by default would punish the common case, and
  silently discarding data is the more dangerous reading.
- **Seeding uses a bulk load path.** The sub-minute target is not reachable row by row at
  this volume. The mechanism is a planning concern; the time budget is the requirement.
- **The seeder does not get its own project.** Constitution P4 caps the solution at four
  projects plus tests. Where the seeding logic lives and how the script invokes it is a
  planning decision, recorded in `research.md`.
- **Token issuance stays out of the API surface.** The service only validates tokens. The
  script mints one for manual use; nothing in the running service issues tokens.
- **The reference date is published.** `docs/performance.md` in feature 003 must cite it
  alongside any measurement, or the numbers are not reproducible by a reader.

## Dependencies

- **Blocks**: feature 003 (the weekly rollup) requires the seeded dataset. Because of
  FR-017, that feature MUST state explicitly whether the rollup includes clients that are
  inactive now but had billable activity in the reported period; the seed guarantees such
  clients exist, so the question cannot be left to be discovered.
- **Blocked by**: feature 001 — the schema, the migration, the container definition and
  the procedure directory must exist before this feature can apply or fill them.
- **External**: container tooling and the pinned SDK.
