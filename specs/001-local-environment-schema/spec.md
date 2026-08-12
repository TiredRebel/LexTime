# Feature Specification: Local Environment and Schema

**Feature Branch**: `001-local-environment-schema`

**Created**: 2026-08-12

**Status**: Draft

**Input**: User description: "Local environment and schema. Scope per `docs/prd.md` §2.1
Infrastructure, §3 Data model, §6 done criteria 1–3: the four `dbo` tables with their
constraints, code-first migrations, a containerised database, an idempotent bootstrap
script that applies migrations and stored procedures and seeds a realistic dataset, plus
bearer-token setup and a health endpoint reporting liveness and database connectivity.
Explicitly not in this spec: the rollup procedure body, CRUD endpoints, domain rule
enforcement in application code."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reviewer runs the project from a cold machine (Priority: P1)

Someone evaluating this repository clones it onto a machine that has only container
tooling and the pinned SDK installed. They read the two commands in the README, run
them, and end up with a running service and a populated database without consulting
anyone, reading source, or discovering an undocumented step.

**Why this priority**: This is the repository's first impression and the binding
done-criterion (`docs/prd.md` §6.1–6.3). Every later feature is unreachable if this
does not work. Constitution P18 makes any undocumented step a defect.

**Independent Test**: On a machine with no prior state, run the two documented
commands in order and request the health endpoint. Fully delivers value on its own:
the reviewer has a working environment even if no other feature exists.

**Acceptance Scenarios**:

1. **Given** a machine with container tooling and the pinned SDK but no project state,
   **When** the reviewer runs the bootstrap command, **Then** it completes without
   error and reports what it created.
2. **Given** the bootstrap has completed, **When** the reviewer starts the service and
   requests the health endpoint, **Then** it responds successfully and reports that the
   database is reachable.
3. **Given** the bootstrap has completed, **When** the reviewer counts rows in each
   table, **Then** the volumes match the seeded figures stated in this spec.
4. **Given** the database container is stopped, **When** the health endpoint is
   requested, **Then** it responds with a failure status naming database connectivity
   as the cause rather than timing out or returning success.

---

### User Story 2 - A developer re-runs the bootstrap safely (Priority: P2)

Someone working on the project runs the bootstrap script again — after a reboot, after
pulling changes, or because they are unsure whether it finished. It does not duplicate
data, does not fail because objects already exist, and does not silently leave the
database in a half-built state.

**Why this priority**: Directly required by `docs/prd.md` §6.1. An
apply-once-only script is a trap for the reviewer in User Story 1 as much as for the
author, but the cold path has to work before the repeat path matters.

**Independent Test**: Run the bootstrap twice in succession and compare row counts and
object definitions between the two runs.

**Acceptance Scenarios**:

1. **Given** a fully bootstrapped environment, **When** the script is run a second time,
   **Then** it completes successfully and row counts are unchanged.
2. **Given** a fully bootstrapped environment, **When** the script is run a second time,
   **Then** stored procedure definitions are replaced rather than duplicated or errored
   on.
3. **Given** an environment where the container is running but no schema has been
   applied, **When** the script is run, **Then** it applies the schema and seeds without
   requiring the container to be recreated.
4. **Given** a request to rebuild from scratch, **When** the script is run with its reset
   option, **Then** the existing data is discarded and the environment is rebuilt.

---

### User Story 3 - The seeded data supports a believable report (Priority: P3)

The dataset that ships with the environment is shaped like real timekeeping activity,
not like uniform random noise, so that the reporting feature built on top of it produces
figures a reader would believe and query plans that differ visibly with and without an
index.

**Why this priority**: Constitution P9. Uniform data produces uniform plans and a
rollup nobody would credit, which undermines the two things this repository exists to
show. It is P3 because a wrongly-shaped seed can be corrected without redoing the
schema.

**Independent Test**: Query the seeded data for the distribution properties listed in
the functional requirements — weekday concentration, client concentration, non-billable
share — and confirm each falls within its stated band.

**Acceptance Scenarios**:

1. **Given** the seeded dataset, **When** entries are grouped by day of week, **Then**
   weekend entries are a small minority rather than two sevenths of the total.
2. **Given** the seeded dataset, **When** clients are ranked by total logged minutes,
   **Then** a small number of clients account for a disproportionate share and a long
   tail accounts for very little.
3. **Given** the seeded dataset, **When** entries are grouped by billable flag, **Then**
   a meaningful minority are non-billable.
4. **Given** the seeded dataset, **When** every entry's duration is checked, **Then**
   all durations satisfy the increment and magnitude rules without exception.

---

### User Story 4 - Protected surface is closed by default (Priority: P4)

A caller without a valid token can reach the health check and the API documentation and
nothing else. The authentication boundary exists and is visible from the outside before
any business endpoint is built on it.

**Why this priority**: The boundary needs to exist before endpoints are added to it, or
each later feature has to remember to opt in. It is last because nothing else in this
spec depends on it.

**Independent Test**: Call the health endpoint and a placeholder protected route with
and without a token and compare the responses.

**Acceptance Scenarios**:

1. **Given** no credentials, **When** the health endpoint is requested, **Then** it
   responds successfully.
2. **Given** no credentials, **When** a protected route is requested, **Then** the
   response is a 401 and no data is disclosed.
3. **Given** a token that is expired or signed with the wrong key, **When** a protected
   route is requested, **Then** the response is a 401 rather than a server error.

---

### Edge Cases

- **Container tooling is not running.** The script must say so in one plain sentence and
  stop, rather than failing deep inside a connection timeout.
- **The database port is already in use.** The failure must name the port conflict.
- **The container is up but the database is not yet accepting connections.** Container
  start and database readiness are different events; the script waits for readiness on a
  bounded timer rather than sleeping a fixed interval and hoping.
- **No stored procedures exist yet.** At the time this spec is implemented,
  `db/programmability/` is empty — the only procedure arrives with feature 002. An empty
  directory is a normal state, not an error.
- **Seeding is interrupted partway.** A subsequent run must either complete the
  environment or report that a reset is required; it must not leave a partially seeded
  database that looks complete.
- **The wrong SDK is the default on the machine.** The pinned version must govern, and
  the failure message when it is absent must name the required version.
- **A time entry would be seeded on a date outside the permitted window.** The generator
  must not produce data that the application's own rules would reject.

## Requirements *(mandatory)*

### Functional Requirements

**Environment**

- **FR-001**: The environment MUST be brought up by a single command that requires no
  arguments for the default path.
- **FR-002**: That command MUST start the database, apply the schema, apply all stored
  procedures found in the programmability directory, and seed data — in that order, and
  MUST report which of those steps it performed.
- **FR-003**: The command MUST be safe to run repeatedly. A second run on a complete
  environment MUST leave row counts unchanged and MUST NOT fail on objects that already
  exist.
- **FR-004**: The command MUST offer an explicit reset option that discards existing
  data and rebuilds. Reset MUST NOT be the default.
- **FR-005**: The command MUST wait for the database to accept connections before
  applying the schema, and MUST fail with a clear message if readiness is not reached
  within a bounded time.
- **FR-006**: Every failure mode listed in Edge Cases MUST produce a message naming the
  cause. A stack trace alone is not a compliant failure.
- **FR-007**: The schema MUST be applied by versioned, code-first migrations. Stored
  procedures MUST NOT be created by migrations; they are applied from source files that
  can be re-applied without being dropped first.

**Data model**

- **FR-008**: The model MUST comprise exactly four entities — timekeepers, clients,
  matters and time entries — with the attributes and types given in `docs/prd.md` §3.
- **FR-009**: A matter MUST belong to exactly one client. A time entry MUST reference
  exactly one timekeeper and exactly one matter.
- **FR-010**: Client codes MUST be unique. Timekeeper email addresses MUST be unique.
  Matter numbers MUST be unique within their client.
- **FR-011**: The database MUST reject, by constraint, any time entry whose duration is
  not positive, is not a multiple of six minutes, or exceeds 1440 minutes. This
  enforcement is required at the storage layer in this feature; the equivalent
  application-layer enforcement belongs to a later feature.
- **FR-012**: Every entity MUST record its creation timestamp. Time entries MUST also
  carry a nullable update timestamp and a rate snapshot copied from the timekeeper.
- **FR-013**: The schema MUST ship with only primary and foreign key indexes. The
  covering index described in `docs/prd.md` §3 MUST NOT be created in this feature — its
  absence is the baseline a later feature measures against.

**Seed data**

- **FR-014**: Seeding MUST produce approximately 25 timekeepers, 60 clients, 220 matters
  and 400,000 time entries spanning 24 months.
- **FR-015**: Seeded time entries MUST concentrate on weekdays, with weekend activity a
  small minority rather than a uniform share.
- **FR-016**: Seeded activity MUST be unevenly distributed across clients — a few large
  clients and a long tail of small ones.
- **FR-017**: A meaningful minority of seeded entries MUST be non-billable.
- **FR-018**: Every seeded entry MUST satisfy the duration and date rules the
  application will later enforce, so that the seed could have been produced through the
  API.
- **FR-019**: Seeding MUST be reproducible: the same inputs MUST produce the same
  dataset, so that performance measurements taken before and after an index change are
  comparable.
- **FR-020**: Seeding MUST complete in well under a minute on a developer machine once
  the database is running.

**Access boundary**

- **FR-021**: The health endpoint and the API documentation MUST be reachable without
  credentials. Every other route MUST require a valid bearer token.
- **FR-022**: An absent, malformed, expired or wrongly-signed token MUST produce a 401
  and disclose nothing about the resource.
- **FR-023**: The health endpoint MUST report both that the service is running and
  whether the database is reachable, and MUST report failure rather than success when
  the database is unreachable.

**Documentation**

- **FR-024**: The quickstart in the README MUST consist of exactly the commands this
  feature provides, with no undocumented prerequisite or manual step.

### Key Entities

- **Timekeeper**: A person who records time. Has a unique email, a display name, a
  default hourly rate, and an active flag. Rate changes do not alter entries already
  recorded.
- **Client**: An organisation being billed. Has a unique short code, a name, and an
  active flag.
- **Matter**: A piece of work for exactly one client. Has a number unique within that
  client, a name, a default billable flag, and an active flag.
- **Time Entry**: A recorded block of work by one timekeeper against one matter. Carries
  the billing date (distinct from the date it was entered), a duration in minutes
  constrained to six-minute increments, a billable flag, the hourly rate captured at
  creation, and a narrative.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person with no prior knowledge of this project reaches a running service
  and a populated database using only the README, in under 10 minutes excluding the
  first-time container image download, and without asking a question.
- **SC-002**: The bootstrap completes in under 3 minutes on a developer machine once the
  container image is present locally, with data generation accounting for under a minute
  of that.
- **SC-003**: Running the bootstrap twice in a row produces identical row counts, and
  the second run reports that it skipped work rather than repeating it.
- **SC-004**: The health check correctly reports failure within 5 seconds of the
  database becoming unreachable, and success within 5 seconds of it returning.
- **SC-005**: Weekend entries are under 10% of all entries; the ten busiest clients
  account for at least half of all logged minutes; non-billable entries are between 10%
  and 25% of the total.
- **SC-006**: 100% of seeded entries satisfy the duration increment, duration magnitude
  and date-window rules — zero exceptions across all 400,000 rows.
- **SC-007**: Two seed runs from the same inputs produce datasets that are identical row
  for row.
- **SC-008**: Every route other than the health check and the documentation returns 401
  without a valid token; 0 routes are unintentionally public.
- **SC-009**: Each documented failure mode produces a message that names its cause, as
  judged by someone who has not read the script.

## Assumptions

Recorded because the feature description did not settle them and a defensible default
existed. Any of these can be overturned in `/speckit-clarify` before planning.

- **Idempotency means skip, not rebuild.** A second run detects a complete environment
  and leaves it alone, reporting what it skipped. Destroying and rebuilding is available
  behind an explicit reset option (FR-004). Reseeding 400,000 rows by default would
  punish the common case, and silently discarding data is the more dangerous of the two
  possible readings.
- **Seeding is deterministic** (FR-019). The generator uses a fixed starting seed rather
  than machine entropy. Constitution P8 requires index before/after numbers to be
  measured and comparable; a dataset that differs between runs makes the comparison
  meaningless.
- **Seed dates are anchored to a fixed reference date**, not to the time the seed runs.
  A 24-month window relative to "now" would drift, breaking reproducibility and
  eventually pushing entries outside the date window the application will enforce.
- **The technology stack is a given, not a decision of this spec.** The container image,
  the ORM, the migration mechanism, the script language and the SDK version are fixed by
  `docs/prd.md` §2.1 and constitution P4, P5 and P7. This spec states behaviour and
  constraints; `/speckit-plan` maps them onto that stack.
- **Seeding uses a bulk path.** FR-020's sub-minute target is not reachable row by row
  at this volume. The mechanism is a planning concern; the time budget is the
  requirement.
- **Timekeepers are seeded and read-only.** No registration or user management exists in
  this feature or in the project (`docs/prd.md` §2.2).
- **Token issuance is out of scope.** The service validates tokens against a symmetric
  development key (`docs/prd.md` §2.2); how a reviewer obtains a token for manual
  testing is a documentation concern for the feature that adds the first protected
  endpoint.

## Dependencies

- **Blocks**: feature 002 (the weekly rollup) requires the seeded dataset and the
  baseline index state defined in FR-013.
- **Blocked by**: nothing. This is the first implementation feature.
- **External**: container tooling and the pinned SDK must be present on the machine.
  Neither is installed by this feature.
