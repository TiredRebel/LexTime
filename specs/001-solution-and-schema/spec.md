# Feature Specification: Solution and Schema

**Feature Branch**: `001-solution-and-schema`

**Created**: 2026-08-12

**Status**: Draft

**Input**: Split from the original `001-local-environment-schema` after the
`/speckit-plan` Constitution Check found it exceeded the one-evening cap in P3. This half
covers the solution structure, the database schema and its storage-level constraints, the
containerised database definition, the access boundary and the health endpoint. The
bootstrap script and data seeding are feature 002.

## Clarifications

### Session 2026-08-12

Carried forward from the original spec. The clarifications about seeded history, seed
composition, reset scope and reviewer token acquisition moved with their scope to feature
002 and are recorded there.

- Q: When the health endpoint is called, what should the caller actually receive — a
  bare status code, or a response body naming each check and its result? → A: A body.
  Success returns 200 and failure returns 503, both carrying a small JSON payload that
  lists each check by name with its status and duration.
- Q: Should seeded historical entries be exempt from the 90-day backdating rule that will
  apply to entries created through the API? → A: Yes. Recorded here because it determines
  something this feature builds: `WorkDate` carries **no** check constraint. The 90-day
  window is a submission rule enforced in application code by a later feature, not a
  storage invariant.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The solution builds clean and runs (Priority: P1)

A developer clones the repository, brings up the database container, applies the schema,
and starts the service. The build produces no warnings, the service serves its API
documentation, and the health endpoint reports green.

**Why this priority**: Nothing else in the project can be built, tested or demonstrated
until this holds. It is also where the build quality gate is established, so every
subsequent feature inherits it rather than adding it.

**Independent Test**: On a machine with container tooling and the pinned SDK, bring up
the container, apply the schema, start the service and request health. Delivers value
alone: a working, verifiably clean skeleton.

**Acceptance Scenarios**:

1. **Given** a clean clone, **When** the solution is built with warnings treated as
   errors, **Then** it succeeds with zero diagnostics.
2. **Given** the database container is running, **When** the schema is applied, **Then**
   all four tables exist with their constraints and no data.
3. **Given** the schema is applied, **When** the service is started and the health
   endpoint requested, **Then** it responds 200 with the database check listed as
   healthy.
4. **Given** a member without a documentation comment is added anywhere in any project,
   **When** the solution is built with warnings treated as errors, **Then** the build
   fails.

---

### User Story 2 - The database enforces its own rules (Priority: P2)

The rules that can be expressed as storage constraints are enforced by the database
itself, so that data written by any route — application, script, or a person with a query
window — is held to them.

**Why this priority**: Constitution P6 requires defence in depth, and feature 002 will
write 400,000 rows through a bulk path that bypasses application validation entirely.
The constraints must exist before anything writes at volume.

**Independent Test**: Attempt each violating insert directly against the database with no
application involved, and confirm rejection.

**Acceptance Scenarios**:

1. **Given** the schema is applied, **When** a time entry with a duration that is zero,
   negative, not a multiple of six, or above 1440 is inserted directly, **Then** the
   database rejects it.
2. **Given** the schema is applied, **When** a time entry with a valid duration is
   inserted directly, **Then** it is accepted.
3. **Given** an existing client, **When** a second client with the same code is inserted,
   **Then** it is rejected.
4. **Given** a matter numbered `001` under client A, **When** a matter numbered `001` is
   inserted under client B, **Then** it is accepted — the number is unique within a
   client, not globally.
5. **Given** a matter numbered `001` under client A, **When** a second matter numbered
   `001` is inserted under client A, **Then** it is rejected.
6. **Given** the schema is applied, **When** a time entry dated three years in the past is
   inserted directly, **Then** it is accepted — no date constraint exists at the storage
   layer.

---

### User Story 3 - Protected surface is closed by default (Priority: P3)

A caller without a valid token can reach the health check and the API documentation and
nothing else. The authentication boundary exists and is visible from outside before any
business endpoint is built on it.

**Why this priority**: The boundary must exist before endpoints are added to it, or each
later feature has to remember to opt in. It is third because nothing else here depends on
it.

**Independent Test**: Call the health endpoint and a placeholder protected route with and
without a valid token and compare responses.

**Acceptance Scenarios**:

1. **Given** no credentials, **When** the health endpoint is requested, **Then** it
   responds successfully.
2. **Given** no credentials, **When** a protected route is requested, **Then** the
   response is 401 and no data is disclosed.
3. **Given** a token that is malformed, expired, or signed with the wrong key, **When** a
   protected route is requested, **Then** the response is 401 rather than a server error.
4. **Given** a validly signed, unexpired token, **When** a protected route is requested,
   **Then** the request is accepted — establishing that the 401s above are the boundary
   working rather than everything being closed unconditionally.

---

### Edge Cases

- **The container is up but the database is not yet accepting connections.** Container
  start and database readiness are different events. Applying the schema against a
  not-yet-ready server must fail with a message naming that, not a bare timeout.
- **The health check runs while the database is unreachable.** It must report failure,
  not succeed because a connection object was constructed.
- **The same matter number exists under two clients.** Must be permitted; a global unique
  index here is the most likely modelling error.
- **A member is added without a documentation comment.** Must fail the build, including
  in the test project.
- **A migration is applied twice.** Must be a no-op, not an error.

## Requirements *(mandatory)*

### Functional Requirements

**Solution structure**

- **FR-001**: The solution MUST comprise exactly four source projects and one test
  project, named and layered per constitution P4.
- **FR-002**: Project references MUST enforce the dependency direction, so that a
  violation is a compile error rather than a review comment. The domain project MUST
  reference no other project and no persistence, web or serialisation package.
- **FR-003**: Each project MUST declare its purpose in a description element in its
  project file.
- **FR-004**: Registration of each layer's services MUST be exposed as a single extension
  method per layer, so that composition reads as a list rather than a wall.

**Build and documentation gate**

- **FR-005**: A single settings file at the repository root MUST apply the analyzer,
  nullable-reference, package-audit and documentation-generation settings to every
  project, including the test project. No project may opt out.
- **FR-006**: Building with warnings treated as errors MUST fail on any analyzer
  diagnostic and on any publicly visible member lacking a documentation comment.
- **FR-007**: Every type, method, property, parameter, return value and documented
  exception MUST carry a documentation comment, private and internal members included.
  A comment that restates its signature does not satisfy this.

**Schema**

- **FR-008**: The model MUST comprise exactly four entities — timekeepers, clients,
  matters and time entries — with the attributes and types given in `docs/prd.md` §3.
- **FR-009**: A matter MUST belong to exactly one client. A time entry MUST reference
  exactly one timekeeper and exactly one matter.
- **FR-010**: Client codes MUST be unique. Timekeeper email addresses MUST be unique.
  Matter numbers MUST be unique **within their client** and MUST NOT be globally unique.
- **FR-011**: The database MUST reject, by constraint, any time entry whose duration is
  not positive, is not a multiple of six minutes, or exceeds 1440 minutes.
- **FR-012**: The billing date MUST NOT carry a constraint. The rules restricting how far
  a date may be backdated or forward-dated govern submission through the API and belong
  to a later feature; enforcing them in storage would reject legitimate history and would
  make the database progressively reject its own contents as time passed.
- **FR-013**: Every entity MUST record its creation timestamp. Time entries MUST also
  carry a nullable update timestamp and a rate snapshot column.
- **FR-014**: The schema MUST ship with only primary and foreign key indexes. The
  covering index described in `docs/prd.md` §3 MUST NOT be created in this feature — its
  absence is the baseline feature 003 measures against.
- **FR-015**: The schema MUST be applied by versioned, code-first migrations, and
  re-applying an already-applied migration MUST be a no-op.
- **FR-016**: Constraints and unique indexes MUST be declared in the model so that they
  are created by the migration and appear in the model snapshot, rather than applied by a
  separate mechanism the model is unaware of.

**Database container**

- **FR-017**: A container definition MUST be committed that runs the database engine
  named in `docs/prd.md` §2.1, with a persistent volume, so that a developer can bring
  the database up with a single command from the tooling.
- **FR-018**: A directory for source-controlled stored procedures MUST exist. It is empty
  in this feature; procedures MUST NOT be created by migrations at any point.

**Access boundary**

- **FR-019**: The health endpoint and the API documentation MUST be reachable without
  credentials. Every other route MUST require a valid bearer token.
- **FR-020**: An absent, malformed, expired or wrongly-signed token MUST produce a 401
  and disclose nothing about the resource.
- **FR-021**: The signing key MUST come from configuration, MUST NOT be hard-coded, and
  the development value MUST live only in the development settings file.
- **FR-022**: No endpoint that issues tokens may be added. The endpoint count in
  `docs/prd.md` §4 stays at seventeen.

**Health**

- **FR-023**: The health endpoint MUST report both that the service is running and
  whether the database is reachable, and MUST report failure rather than success when the
  database is unreachable.
- **FR-024**: The health response MUST use status code 200 when every check passes and
  503 when any check fails. A degraded system MUST NOT return 200.
- **FR-025**: Both responses MUST carry a JSON body listing each check by name with its
  individual status and duration, so a caller can identify which component failed without
  access to logs.
- **FR-026**: The database check MUST verify that a query can actually be executed, not
  merely that a connection object was constructed.
- **FR-027**: The health response MUST NOT disclose the connection string, credentials,
  server hostname or a stack trace. The endpoint is unauthenticated; anything it returns
  is public.

**Tests**

- **FR-028**: A test project MUST exist and MUST run against a real instance of the
  database engine in a container. No in-memory or file-based database provider may be
  referenced anywhere in it.
- **FR-029**: Tests MUST cover the storage constraints in FR-010 and FR-011, the health
  behaviour in FR-023 to FR-026, and the boundary behaviour in FR-019 and FR-020.

### Key Entities

- **Timekeeper**: A person who records time. Unique email, display name, default hourly
  rate, active flag. Rate changes do not alter entries already recorded.
- **Client**: An organisation being billed. Unique short code, name, active flag.
- **Matter**: A piece of work for exactly one client. Number unique within that client,
  name, default billable flag, active flag.
- **Time Entry**: A recorded block of work by one timekeeper against one matter. Billing
  date, duration in minutes constrained to six-minute increments, billable flag, hourly
  rate captured at creation, narrative.

The active flag is forward-looking on all three entities that carry it: setting it false
prevents new entries being recorded, and does not remove, hide or invalidate entries
recorded while it was true. No other state transitions exist — there is no approval,
submission or lock workflow, and no soft delete.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer with container tooling and the pinned SDK reaches a running
  service and a green health check using only the repository's documentation, in under 10
  minutes excluding the first-time image download.
- **SC-002**: Building with warnings treated as errors produces zero diagnostics across
  all five projects.
- **SC-003**: Adding an undocumented public member to any project, including the test
  project, fails that build — verified by doing it once.
- **SC-004**: The health check reports failure within 5 seconds of the database becoming
  unreachable and success within 5 seconds of it returning, and in the failure case names
  the database check specifically.
- **SC-005**: All six storage-constraint scenarios in User Story 2 behave as specified
  when executed directly against the database with no application involved.
- **SC-006**: Every route other than the health check and the documentation returns 401
  without a valid token; 0 routes are unintentionally public.
- **SC-007**: Applying the migration to an already-migrated database completes
  successfully and changes nothing.
- **SC-008**: The domain project's compiled output has no dependency on any persistence,
  web or serialisation package — verifiable from its project file alone.

## Assumptions

- **The technology stack is a given, not a decision of this spec.** The container image,
  the ORM, the migration mechanism and the SDK version are fixed by `docs/prd.md` §2.1 and
  constitution P4, P5 and P7. This spec states behaviour and constraints.
- **Bringing the container up and applying the schema are manual in this feature.** Two
  commands from the container tooling and the ORM tooling respectively. Automating them
  into a single scripted path is feature 002; until then the developer runs them.
- **Timekeepers are seeded and read-only**, and no registration, identity provider or
  password flow exists anywhere in the project.
- **Tests mint their own tokens.** User Story 3's fourth scenario needs a valid token;
  the test project constructs one with the development key. The reviewer-facing token,
  printed by the bootstrap script, arrives with feature 002.
- **The placeholder protected route is temporary.** Something must exist to prove the
  boundary rejects and accepts. It is removed when the first real endpoint lands, and it
  is not counted in the seventeen.

## Dependencies

- **Blocks**: feature 002 (bootstrap and seed) needs the schema, the migration and the
  container definition. Feature 003 (the weekly rollup) needs the unindexed baseline in
  FR-014.
- **Blocked by**: nothing. This is the first implementation feature.
- **External**: container tooling and the pinned SDK must be present. Neither is
  installed by this feature.
