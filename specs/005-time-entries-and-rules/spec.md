# Feature Specification: Time Entries and the Domain Rules

**Feature Branch**: `005-time-entries-and-rules`

**Created**: 2026-08-14

**Status**: Draft

**Input**: The write path, and the six rules `docs/prd.md` §2.1 calls "deliberately few, but real".
Covers the five time-entry endpoints of §4 and done criterion §6.4 — every rule with a rejecting
*and* an accepting test. CRUD for clients, matters and timekeepers is feature 006; the seed
already supplies all three, so nothing here waits on it.

## Clarifications

### Session 2026-08-14

- Q: When an existing entry is updated, do the backdating window (rule 4) and the active-matter
  requirement (rule 5) re-apply? → A: Only to fields actually being changed. Rules 1, 2, 3 and 6
  re-apply always. Rule 4 is evaluated only when the work date is being changed, and rule 5 only
  when the matter is being changed. This follows what those two rules are for: rule 4 governs
  which *work dates may be submitted* and rule 5 which *matters may be billed to*, and leaving a
  field alone is not submitting a value for it. It prevents the abuses that matter — moving time
  onto a closed matter, backdating past the window — while still allowing a typo in a year-old
  narrative to be fixed. Validating the whole entry as if newly recorded would freeze every entry
  older than 90 days, typos included; skipping both rules entirely would leave rule 5 defeatable
  by editing rather than creating. Deletion stays unrestricted: `docs/prd.md` §2.2 rules out a
  locking workflow and restricting removal would be a step toward one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A timekeeper records time, and a bad entry is refused with a reason (Priority: P1)

Someone logs six minutes against a matter and it is recorded, with the rate they bill at
captured onto it. Someone else logs seven minutes, or twenty-six hours, or time against a matter
that closed last year — and each is refused with a message naming what was wrong and what the
rule is.

**Why this priority**: This is the feature. The five endpoints are ordinary; the six rules are
the reason `docs/prd.md` describes the domain as "deliberately few, but real", and they are what
a reviewer reads this code to see. Nothing else here is worth building first.

**Independent Test**: against the seeded database, submit one conforming entry and one violating
entry per rule, and inspect what comes back. Delivers value alone — time can be recorded and bad
data cannot enter.

**Acceptance Scenarios**:

1. **Given** an active matter of an active client and a valid duration, **When** an entry is
   submitted, **Then** it is recorded and returned with its identifier and the rate captured
   from the timekeeper.
2. **Given** a duration that is not a positive multiple of six minutes, **When** an entry is
   submitted, **Then** it is refused with a message naming the value and the increment rule.
3. **Given** a duration above the single-entry maximum, **When** an entry is submitted, **Then**
   it is refused naming the value and the maximum.
4. **Given** a timekeeper whose entries for a date already total the daily maximum, **When**
   another entry for that date is submitted, **Then** it is refused, naming the total already
   recorded.
5. **Given** a work date in the future, **When** an entry is submitted, **Then** it is refused.
6. **Given** a work date older than the backdating limit, **When** an entry is submitted,
   **Then** it is refused, naming the limit.
7. **Given** a matter that is inactive, or an active matter belonging to an inactive client,
   **When** an entry is submitted, **Then** it is refused, and the message says which of the two
   was inactive.
8. **Given** a recorded entry, **When** the timekeeper's rate is subsequently changed, **Then**
   the entry's captured rate is unchanged.

---

### User Story 2 - A timekeeper corrects an entry they got wrong (Priority: P2)

Time gets logged against the wrong matter, or with the wrong duration, or with a narrative that
says "misc". The person who recorded it can change it, or remove it entirely, and the same rules
apply to the corrected version as applied to the original.

**Why this priority**: An entry system without correction is not usable, and a correction path
that skips the rules is a hole straight through them. It is P2 because recording has to work
before correcting means anything.

**Independent Test**: record an entry, change each of its fields in turn, and confirm the rules
are applied to the result; then remove it and confirm it is gone.

**Acceptance Scenarios**:

1. **Given** a recorded entry, **When** its duration, narrative, billable flag or matter is
   changed to a conforming value, **Then** the change is saved.
2. **Given** a recorded entry, **When** a change would violate any rule that applies to updates,
   **Then** it is refused and the stored entry is unchanged.
3. **Given** a recorded entry, **When** its duration is raised, **Then** the daily maximum is
   evaluated against the day's other entries plus the new value — the entry's own previous
   duration is not counted twice.
4. **Given** a recorded entry, **When** it is updated, **Then** the captured rate is not
   recalculated from the timekeeper's current rate.
5. **Given** a recorded entry, **When** it is deleted, **Then** it no longer appears in any
   listing or report and the identifier is not reused.
6. **Given** an identifier that matches no entry, **When** it is fetched, updated or deleted,
   **Then** the response says so rather than failing obscurely.

---

### User Story 3 - A timekeeper finds the entries they are looking for (Priority: P3)

Someone wants their own entries for last week, or everything logged against one matter. They
filter by timekeeper, by matter, by date range, and page through the result.

**Why this priority**: Necessary to use the system and to confirm the other two stories did what
they claimed, but it introduces no rule and carries no risk of corrupting data. `docs/prd.md` §7
names the plain endpoints as the first thing to cut, and this is the plain part of this feature.

**Independent Test**: with the seeded dataset, request entries with each filter alone and in
combination, and page through a filtered result.

**Acceptance Scenarios**:

1. **Given** the seeded dataset, **When** entries are requested filtered by timekeeper, **Then**
   only that timekeeper's entries are returned.
2. **Given** the same, **When** filtered by matter and by date range together, **Then** both
   filters apply.
3. **Given** more matching entries than one page holds, **When** successive pages are requested,
   **Then** every entry appears exactly once across the pages and none is skipped.
4. **Given** any request with no filters, **When** it is made, **Then** the result is bounded by
   a default page size rather than returning the whole table.
5. **Given** a single entry's identifier, **When** it is fetched, **Then** that entry is
   returned with its captured rate.

---

### Edge Cases

- **An entry submitted for today, at the boundary of the daily maximum.** Exactly at the limit is
  accepted; one increment beyond is refused. The boundary is a value, not a region.
- **The oldest permitted work date, and the day before it.** Same: the boundary itself is
  accepted.
- **A work date of today.** Accepted — "not in the future" includes today.
- **Two entries submitted for the same timekeeper and date at the same moment**, which together
  would exceed the daily maximum. The rule must not be defeated by timing.
- **An entry against an active matter whose client has since gone inactive.** Rule 5 concerns
  both; the refusal has to say which one failed or the caller cannot act on it.
- **A timekeeper who is inactive.** Not one of the six rules as written. Covered by FR-013.
- **Updating an entry whose work date is now outside the backdating window**, though it was
  inside when recorded. The subject of FR-016.
- **A duration of zero, or negative.** Not a positive multiple of six; refused by rule 1 rather
  than needing a rule of its own.
- **A listing filtered to a range containing nothing.** An empty page, not an error.
- **A page size of zero, or enormous.** Bounded rather than honoured literally.

## Requirements *(mandatory)*

### Functional Requirements

**Recording**

- **FR-001**: An entry MUST be recordable with a timekeeper, a matter, a work date, a duration in
  minutes, a billable flag and a narrative.
- **FR-002**: On recording, the timekeeper's current rate MUST be captured onto the entry.
- **FR-003**: A recorded entry MUST be returned with its identifier and its captured rate, so the
  caller need not re-fetch to learn either.

**The six rules**

- **FR-004** *(rule 1)*: Duration MUST be a positive whole number of minutes and a multiple of
  six. Zero and negatives fail this rule; no separate rule is needed for them.
- **FR-005** *(rule 2)*: A single entry's duration MUST NOT exceed the equivalent of 24 hours.
- **FR-006** *(rule 3)*: A timekeeper's total recorded minutes for one work date MUST NOT exceed
  the equivalent of 24 hours. When an existing entry is changed, its own current duration MUST be
  excluded from the total it is checked against — otherwise raising a duration counts the entry
  twice and the rule refuses changes it should permit.
- **FR-007** *(rule 4)*: A work date MUST NOT be in the future, and MUST NOT be more than 90 days
  in the past. Today is permitted; the ninetieth day is permitted.
- **FR-008** *(rule 5)*: An entry MUST be recorded only against an active matter belonging to an
  active client. The refusal MUST say which of the two was inactive — a caller told only "not
  active" cannot tell whether to reopen a matter or a client.
- **FR-009** *(rule 6)*: The captured rate MUST NOT change when the timekeeper's rate later
  changes, and MUST NOT be recalculated when the entry is updated. History is not rewritten by a
  pay rise.
- **FR-010**: Every refusal MUST name the rule that was broken and the value that broke it, in
  the machine-readable problem format the rest of the API uses. "Invalid request" is not
  compliance with this requirement.
- **FR-011**: The rules MUST be enforced in one place and reached by every path that records or
  changes an entry. A rule implemented once per endpoint is a rule that will eventually be
  enforced in only some of them.
- **FR-012**: The storage-level constraints on duration MUST remain in force. The C# checks are
  additional to them, not a replacement — constitution P6 makes that duplication deliberate, and
  removing the constraint because "the application checks it now" is a defect.
- **FR-013**: An entry MUST NOT be recorded against an inactive timekeeper. Not one of the six
  rules as PRD §2.1 lists them, but the same reasoning as rule 5, and its absence would let
  someone who has left the firm keep logging time.

**Correcting**

- **FR-014**: A recorded entry's matter, work date, duration, billable flag and narrative MUST be
  changeable.
- **FR-015**: A rejected change MUST leave the stored entry exactly as it was. A partially
  applied update is worse than a refused one.
- **FR-016**: When an existing entry is updated, rules 1, 2, 3 and 6 MUST re-apply in every case.
  Rule 4 MUST be evaluated **only when the work date is being changed**, and rule 5 **only when
  the matter is being changed**. An update that leaves a field untouched is not a submission of
  that field, so an entry whose work date has since aged past the window may still have its
  narrative, duration or billable flag corrected — but may not have its work date moved, and may
  not be reassigned to a matter or client that is no longer active.
- **FR-017**: Deletion MUST NOT be restricted by the backdating window or by the matter's current
  status. Any entry may be removed. `docs/prd.md` §2.2 rules out a locking workflow, and gating
  removal on a period rule would be the first half of one.
- **FR-018**: An entry MUST be deletable outright, leaving no trace and no reuse of its
  identifier.
- **FR-019**: A request naming an identifier that matches no entry MUST say so distinctly, rather
  than reporting a generic failure or an empty success.

**Finding**

- **FR-020**: Entries MUST be listable, filtered by any combination of timekeeper, matter and
  work-date range.
- **FR-021**: Listings MUST be paged, with a default bound applied when the caller asks for none,
  and an upper bound applied when the caller asks for more than it. An unfiltered, unbounded
  request MUST NOT return the whole table.
- **FR-022**: Listing order MUST be deterministic, so paging cannot skip or repeat an entry
  across successive requests.
- **FR-023**: A single entry MUST be retrievable by its identifier, carrying its captured rate.

**Access and verification**

- **FR-024**: Every endpoint in this feature MUST require authentication, inheriting the
  boundary established in feature 001. None may be anonymous.
- **FR-025**: Each of the six rules MUST have both a test that shows it refusing a violation and
  a test that shows it accepting a conforming value. A rule proved only to refuse could be
  refusing everything (`docs/prd.md` §6.4).
- **FR-026**: The tests for rule 4 MUST be deterministic as the calendar moves. A test asserting
  that a fixed date is within the backdating window passes today and fails in three months, and
  a test suite that rots on a date is worse than no test because it fails while nothing is wrong.

### Key Entities

- **Time entry** *(existing)*: gains a write path. A timekeeper, a matter, a work date, a
  duration in minutes, a billable flag, a narrative, and a rate captured at recording. The
  captured rate is the field that makes it a historical record rather than a projection.
- **Timekeeper** *(existing, read-only here)*: supplies the rate to capture and must be active
  for an entry to be recorded against them.
- **Matter and client** *(existing, read-only here)*: both must be active. The pair is what rule
  5 checks; feature 006 will make them writable.
- **Rule violation**: not stored, but a first-class result. Carries which rule failed, the value
  that failed it, and enough context to act — a refusal a caller cannot act on has failed at its
  only job.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All six rules have a refusing test and an accepting test — twelve tests minimum,
  and zero rules covered by only one of the two.
- **SC-002**: Each rule's boundary is tested at the exact limit and one step beyond, and the
  limit itself is accepted in every case.
- **SC-003**: A refused submission leaves the stored data byte-for-byte unchanged, confirmed by
  comparing before and after.
- **SC-004**: Every refusal message identifies the rule and the offending value; judged by
  someone who has not read the code and can say what to change.
- **SC-005**: The captured rate on an entry is unchanged after the timekeeper's rate is altered
  and the entry is subsequently updated.
- **SC-006**: Paging across a filtered listing returns every matching entry exactly once, with no
  entry skipped or repeated, verified across at least three pages.
- **SC-007**: An unfiltered listing returns no more than the default page size, regardless of how
  many entries exist.
- **SC-008**: No endpoint in this feature returns data to an unauthenticated caller.
- **SC-009**: The rule tests pass unchanged when the system clock is moved forward by a year.
- **SC-010**: The storage-level duration constraints still reject a violating write made outside
  the application, proving the defence in depth is real and not merely claimed.

## Assumptions

- **The rules bind at recording and at correction, not retroactively.** Existing seeded history
  is not re-validated and does not become invalid. Feature 002 settled this for the backdating
  window; the same reasoning covers the rest.
- **The daily maximum counts every entry for that timekeeper and date**, billable or not. Work is
  work; the billable flag decides what is charged, not what is possible.
- **The daily maximum is evaluated against what is stored**, so two simultaneous submissions that
  each pass individually are the interesting case. How that is prevented is a planning decision;
  that it must not be defeasible by timing is the requirement.
- **The backdating window is inclusive at both ends**, counted in whole days against the current
  date. Today is in; the ninetieth day back is in; the ninety-first is out.
- **"Active" is the current flag**, read when the entry is recorded or changed. There is no
  history of activation and none is needed.
- **A caller may record time for any timekeeper.** There is no ownership model: `docs/prd.md`
  §2.2 rules out RBAC, and a token here proves the caller is trusted, not who they are.
- **Narrative is required but unconstrained** beyond its stored length. No rule in §2.1 governs
  it.
- **Where the rules live in the code is a planning decision.** Constitution P6 requires them in
  the domain rather than scattered across endpoints, and P4 fixes the layering; the concrete
  shape belongs in `plan.md`.

## Out of Scope

Named so the boundary reads as chosen rather than missed.

| Not in this feature | Why |
| --- | --- |
| CRUD for clients, matters and timekeepers | Feature 006. The seed supplies all three, so nothing here is blocked by their absence |
| Any change to the rollup, its procedure or its index | Feature 004 measured it. A write-path change in the same feature would put that measurement in question |
| Approval, submission or locking workflow | `docs/prd.md` §2.2 — a state machine and three times the endpoints for no new technical signal |
| Soft delete and audit trail | `docs/prd.md` §2.2 — timestamps only |
| Ownership, roles or per-user permissions | `docs/prd.md` §2.2 — the token proves trust, not identity |
| Rate cards, rate history, matter-level rates | `docs/prd.md` §2.2 — one captured rate on the entry proves the point |
| Bulk or batch entry submission | Not in §4's endpoint list, and the interesting rules are per entry |

## Dependencies

- **Blocked by**: feature 001 for the schema, the access boundary and the error format; feature
  002 for the seeded timekeepers, clients and matters this feature records against and tests with.
- **Blocks**: feature 006, which makes clients and matters writable and will need rule 5 to
  already exist so that deactivating a client has a defined effect.
- **Interacts with**: feature 003's rollup, which reads what this feature writes. The rollup is
  not modified, but entries created here MUST be visible to it — an entry the write path accepts
  and the report cannot see would be a defect in this feature.
- **External**: none beyond what the quickstart already requires.
