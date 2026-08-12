# Specification Quality Checklist: Local Environment and Schema

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-12
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`

### Validation record

Two iterations were run.

**Iteration 1 — three failures, all fixed:**

1. *No implementation details* — failed. The first draft named the container image, the
   ORM, the bulk-copy mechanism and the script language throughout the functional
   requirements. Rewritten to state behaviour and constraints only; the stack is
   recorded in Assumptions as fixed by `docs/prd.md` §2.1 and constitution P4/P5/P7
   rather than chosen here. FR-020 now states the time budget and leaves the mechanism
   to planning.
2. *Success criteria are technology-agnostic* — failed. Draft criteria referenced
   migration counts and connection behaviour. Rewritten around what an observer can
   measure: time to a working environment, row counts unchanged across runs,
   distribution bands, percentage of routes closed.
3. *Requirements are testable* — partially failed. "Realistic seed data" was not
   testable. Split into FR-015 to FR-018 with SC-005 giving numeric bands (weekend under
   10%, top ten clients at least half of logged minutes, non-billable 10–25%).

**Iteration 2 — all items pass.**

### Clarifications not raised at authoring time

Two ambiguities were resolved by informed default rather than by a `[NEEDS
CLARIFICATION]` marker, because a defensible default existed in each case. Both survived
the `/speckit-clarify` session unchanged and remain recorded in the spec's Assumptions
section:

- **What "idempotent" means for seeded data** — resolved as skip-and-report, with reset
  behind an explicit option. The alternative reading (rebuild every run) would discard
  data by default, which is the more dangerous of the two. Session Q5 refined the scope
  of that reset rather than overturning it.
- **Whether seeding is deterministic** — resolved as deterministic with a fixed seed and
  a fixed date anchor. Constitution P8 requires the index before/after measurement to be
  comparable, which a varying dataset would prevent.

### `/speckit-clarify` session, 2026-08-12

Five questions asked and answered; all five integrated. One resolved a genuine
contradiction in the authored spec:

- **Q1 — seeded history vs the 90-day backdating rule.** FR-018 as written required every
  seeded entry to satisfy a rule forbidding dates more than 90 days old, while FR-014
  required a 24-month span. Roughly seven eighths of the dataset would have violated the
  spec. Resolved by separating creation-time rules from stored-data invariants: FR-018
  narrowed to duration, FR-018a added to state the exemption. This was a defect in the
  spec, not an ambiguity in the request.
- **Q2 — inactive rows in the seed.** FR-020a, FR-020b, SC-010 added. Also propagated
  forward: the Dependencies section now requires feature 002 to state whether the rollup
  includes clients that are inactive now but billed during the reported period.
- **Q3 — health response contract.** FR-023a/b/c added; SC-004 and User Story 1's fourth
  scenario tightened to require the failing check be named in the response.
- **Q4 — reviewer token acquisition.** FR-022a/b and SC-011 added. Previously deferred to
  a later feature, which left this spec's own User Story 4 unverifiable by hand.
- **Q5 — reset scope.** FR-004a/b/c added. Reset drops the database only; full teardown
  is delegated to the container tooling's existing command rather than reimplemented.

### Constitution alignment

Checked against `.specify/memory/constitution.md` v2.0.0:

- **P3** (one evening per spec) — scope is one evening: schema, migrations, container,
  bootstrap, seed, health, token validation. The rollup, CRUD and application-layer rule
  enforcement are excluded explicitly.
- **P6** — FR-011 places the increment and magnitude rules in the schema. The
  application-layer half of that defence-in-depth belongs to the time-entry feature and
  is named as out of scope here.
- **P7** — FR-002 and FR-007 require procedures to be applied from source files and kept
  out of migrations.
- **P8** — FR-013 keeps the covering index out of this feature so a later one has a
  genuine baseline; FR-019 makes the two measurements comparable.
- **P9** — FR-015 to FR-017, with numeric bands in SC-005.
- **P18** — FR-024 and SC-001.
- **P14** — no implementation code appears in this spec.
