# Phase 0 Research: Solution and Schema

**Feature**: 001-solution-and-schema | **Date**: 2026-08-12

The stack is fixed by `docs/prd.md` §2.1 and constitution P4, P5 and P7, so this document
does not re-decide it. It records the decisions this plan had to make, each of which had
more than one defensible answer.

Decisions about the seeder, deterministic generation, readiness polling, procedure
application and token minting moved with their scope to
[feature 002](../002-bootstrap-and-seed/research.md).

---

## R1. Health check shape

**Decision**: The platform's built-in health check registration with one named database
check that executes a trivial query. A custom response writer emits the JSON body required
by FR-025; the default writer returns a bare status string and does not meet the contract.

**Rationale**: FR-024 and FR-026. The distinction in FR-026 matters more than it looks —
constructing a connection object succeeds against a server that is not running, so a check
that stops there reports healthy while the database is down. That is the failure the
requirement exists to prevent, and it is the one a naive test will not catch.

**Alternatives considered**:

- *A hand-rolled endpoint.* More code for the same result, and it would not participate in
  the readiness conventions a deployment probe expects.
- *The default response writer.* Returns overall status only; FR-025 requires per-check
  detail so a caller can identify which component failed without log access.

**Consequence**: the database check needs a connection and command timeout short enough
that a failing check returns inside the five-second window in SC-004, rather than sitting
on a default timeout.

---

## R2. Constraints in the model versus in raw DDL

**Decision**: Check constraints and unique indexes are declared in the EF model
configuration so they are created by the initial migration and appear in the model
snapshot.

**Rationale**: Constitution P6 requires the increment and magnitude rules to exist in the
schema, and P7's ban on migration-authored SQL is specifically about *stored procedures* —
table constraints are exactly what migrations are for. Declaring them in the model keeps
the schema and the model from drifting; a constraint applied by a side channel is one the
model believes does not exist, and the next migration will happily contradict it.

**Alternatives considered**:

- *Constraints in a hand-written `.sql` file applied alongside procedures.* Splits the
  schema across two mechanisms and lets a fresh migration silently omit them.
- *Application-layer validation only.* Fails P6 outright, and feature 002 writes 400,000
  rows through a bulk path that bypasses application validation entirely — the constraints
  must exist before anything writes at volume.

**Watch item**: the matter number uniqueness is composite — `(ClientId, MatterNumber)`, not
a global unique index on `MatterNumber`. Two clients may each have a matter numbered
`001`. This is the single most likely modelling error in the feature and has its own test.

---

## R3. What the billing date must *not* have

**Decision**: No check constraint on `WorkDate`. The only date rule enforced anywhere in
this feature is none.

**Rationale**: The 90-day backdating rule governs what may be *submitted*, not what may
*exist*. A constraint would reject the historical data feature 002 writes, and would make
the database progressively reject its own contents as time passed — a row legal on the day
it was written becomes illegal ninety-one days later, so a restore or a schema rebuild
would fail against data the database itself produced.

This was a contradiction in the first draft of the original spec, caught in clarification
Q1. `docs/prd.md` §3 already drew the line correctly: a `CHECK` on `DurationMinutes`, none
on `WorkDate`.

**Alternatives considered**:

- *A constraint permitting anything within 24 months.* Still fails on the same
  time-passing argument, just more slowly, and encodes a seeding detail as a schema rule.

**Consequence**: FR-012 is a requirement that something be *absent*, which is easy to
"fix" by mistake. It has an explicit positive test — inserting a three-year-old entry and
asserting acceptance — so that adding the constraint later breaks a test rather than
silently breaking feature 002.

---

## R4. Documentation and analyzer settings scope

**Decision**: One `Directory.Build.props` at the repository root applies
`AnalysisMode=Recommended`, `AnalysisModeSecurity=All`, `EnforceCodeStyleInBuild=true`,
`Nullable=enable`, `NuGetAudit=true` and `GenerateDocumentationFile=true` to every project
including the test project. No project opts out.

**Rationale**: Constitution P23 and P25. P25 exempts nothing, and a per-project opt-out
would be exactly the quiet erosion the principle exists to prevent. One file rather than
five `.csproj` edits also means a later project inherits the gate by existing.

**Consequence, stated rather than discovered**: every test method needs an XML summary,
roughly a dozen in this feature. If that becomes friction at scale, the correct response is
an amendment to P25 with an explicit test-project clause — not a
`GenerateDocumentationFile=false` line in one `.csproj`.

**Verification**: SC-003 requires deliberately adding an undocumented member once and
confirming the build fails. A gate nobody has seen fire is a gate nobody knows is wired up.

---

## R5. Proving the auth boundary before any endpoint exists

**Decision**: A single placeholder protected route exists for the life of this feature
only, purely so the boundary can be shown to reject *and* accept. It is removed when the
first real endpoint lands and is not counted in the seventeen in `docs/prd.md` §4.

**Rationale**: A boundary that only ever returns 401 is indistinguishable from a service
that is entirely broken. User Story 3's fourth scenario — a valid token being accepted — is
what separates "closed correctly" from "closed unconditionally", and it needs something to
call.

**Alternatives considered**:

- *Testing the boundary against the health endpoint.* Health is deliberately
  unauthenticated, so it proves nothing about the protected path.
- *Waiting until feature 004 to test auth at all.* Leaves the boundary unverified through
  two features, and makes the first CRUD feature responsible for discovering that token
  validation was misconfigured.

**Security review item (P24)**: token validation has insecure defaults worth checking by
hand rather than trusting — that the signing key comes from configuration and is not
hard-coded, that the accepted algorithm is restricted rather than inferred from the token
header, and that lifetime and audience validation are actually enabled.

---

## Unknowns remaining

None. No `NEEDS CLARIFICATION` markers were carried into this plan.
