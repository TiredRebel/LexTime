# Specification Quality Checklist: Clients, Matters and Timekeepers

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-15
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- All 16 items pass. The one open question — whether a client's code and a matter's number may be
  changed after creation — was answered in the Clarifications session of 2026-08-15 and closed the
  three items that depended on it. It was put to a decision rather than defaulted because both
  readings are defensible and they produce different update contracts, a different collision path
  and different tests. The answer removed a path rather than adding one: **FR-012** now records
  that a collision is reachable only on creation, so there is no update-side collision handling to
  write, test, or later find unreachable.
- **FR-013 was decided rather than asked.** Deactivating a client does not cascade to its matters,
  because feature 005's rule 5 already reads both flags independently and its refusal names which
  one failed — a cascade would make the "client is inactive" branch unreachable, and the seeded
  data already contains active matters of inactive clients. The existing design answers the
  question, so asking it again would be asking the user to re-decide something already shipped.
- **FR-006 is a decision the schema does not make.** SQL Server's default collation is
  case-insensitive, so `ACME` and `acme` would collide anyway — but that is an accident of
  configuration rather than a stated rule, and a reader should not have to know the collation to
  predict the API. Stated explicitly so it survives a collation change.
- **SC-011 asks the storage constraints to be re-proved**, on the same reasoning as feature 005's
  SC-010: adding an application-level check is exactly when someone concludes the database
  constraint is redundant.
