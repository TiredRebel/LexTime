# Specification Quality Checklist: Bootstrap and Seed

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

### Provenance

This spec is the second half of the original `001-local-environment-schema`, split after
that spec's `/speckit-plan` Constitution Check failed **P3** (one evening per spec). The
solution structure, schema, container definition, access boundary and health endpoint are
[feature 001](../../001-solution-and-schema/spec.md).

The split follows the original's own user story boundaries — its User Story 2
(safe re-run) and User Story 3 (believable seed shape) moved here whole, and its User
Story 1 split at the point where a manual three-command path becomes a scripted
two-command one.

### Clarifications already resolved

Four of the five questions from the original `/speckit-clarify` session concern work in
this feature and are recorded in the spec's Clarifications section:

- **Q1 — seeded history vs the 90-day rule.** This one found a genuine contradiction: the
  original spec required a 24-month span *and* required every entry to satisfy a rule
  forbidding dates over 90 days old. FR-019 now states the exemption explicitly, and
  feature 001 carries the schema-side consequence — `WorkDate` gets no constraint.
- **Q2 — inactive rows in the seed.** FR-016, FR-017, SC-007. Propagated forward: the
  Dependencies section requires feature 003 to state whether the rollup includes clients
  that are inactive now but billed during the reported period.
- **Q4 — reviewer token acquisition.** FR-024, FR-025, SC-008.
- **Q5 — reset scope.** FR-005 to FR-008. Reset drops the database only; the full teardown
  is delegated to the container tooling's existing command rather than reimplemented.

Q3 (health response contract) concerns feature 001 and is recorded there.

Two assumptions made at authoring time without raising a marker also survive here:
idempotency means **skip, not rebuild**, and seeding is **deterministic** with a fixed
generator seed and a fixed date anchor.

### Constitution alignment

Not yet gated — `/speckit-plan` has not run for this feature. Phase 0 decisions carried
forward from the original planning run are in [research.md](../research.md) and the
bootstrap contract is in [contracts/bootstrap-cli.md](../contracts/bootstrap-cli.md);
both were produced against constitution v2.0.0 and should be validated rather than
re-derived.

Points the gate will need to examine:

- **P4** — the seeder must not become a fifth project. R1 in `research.md` resolves this
  by putting the logic in `Infrastructure` and invoking it through the API entry point.
- **P7** — procedures applied from source files, never by a migration. FR-010.
- **P8** — FR-020 and FR-021 exist to make feature 003's index measurement comparable;
  a non-deterministic seed would silently invalidate it.
- **P18** — this feature is what makes the two-command quickstart true. FR-026.
- **P23/P24** — executing procedure files makes the command text non-literal, which raises
  CA2100. R4 records the suppression and flags it as a manual security review item rather
  than analyzer noise.
- **P3** — the remaining scope is one evening. If planning finds otherwise, the same
  response applies: split, do not waive.
