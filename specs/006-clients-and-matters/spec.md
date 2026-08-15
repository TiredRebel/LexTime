# Feature Specification: Clients, Matters and Timekeepers

**Feature Branch**: `006-clients-and-matters`

**Created**: 2026-08-15

**Status**: Draft

**Input**: The ten remaining endpoints of `docs/prd.md` §4 — the parties time is recorded
against. Until now they have existed only as seeded rows; this feature lets them be created,
corrected and closed. Feature 005 built the write path and the six rules that read these
records' active flags, so what deactivation *means* is decided here rather than there.

## Clarifications

### Session 2026-08-15

- Q: May a client's code and a matter's number be changed after creation? → A: No — both are
  immutable once set. Update changes a name, a default billable flag and an active flag, and
  nothing else. Codes and numbers are how the firm refers to these records *outside* this system
  — on invoices, in correspondence, in other software — so changing one silently breaks every
  reference held elsewhere, and a caller who cached a code would find it stops resolving with
  nothing to tell them why. It also keeps the update path free of any collision case: uniqueness
  can fail in exactly one place, which is one path to get right rather than two. A genuine typo
  is corrected by opening a correctly-coded record and closing the wrong one, which leaves an
  honest trail rather than rewriting one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A firm takes on a client and opens its first matter (Priority: P1)

Someone registers a new client with a short code the firm will refer to it by, then opens a
matter under it. From that moment time can be recorded against the matter. If the code is
already in use, or the matter number is already used under that client, they are told exactly
that rather than being handed a database failure.

**Why this priority**: Without it the system can only ever bill the clients the seed happened to
create. It is also where the collisions live — the two uniqueness rules the schema has enforced
since feature 001 but that no caller has ever been able to trip.

**Independent Test**: register a client, open a matter under it, record time against that matter
through the existing endpoint, and confirm it is accepted. Then repeat the registration and
confirm the collision is reported.

**Acceptance Scenarios**:

1. **Given** a code no client is using, **When** a client is registered, **Then** it is created
   and returned with its identifier, active.
2. **Given** a code another client already holds, **When** a client is registered with it,
   **Then** the request is refused and the message names the code as the conflict.
3. **Given** an existing client, **When** a matter is opened under it with an unused number,
   **Then** it is created and returned, active.
4. **Given** a client that already has a matter numbered `001`, **When** another matter numbered
   `001` is opened under **that same client**, **Then** it is refused naming the number.
5. **Given** two different clients, **When** each opens a matter numbered `001`, **Then** both
   succeed — matter numbers are unique within a client, not across the firm.
6. **Given** a newly opened matter of a newly registered client, **When** time is recorded
   against it, **Then** it is accepted.
7. **Given** a client identifier matching nothing, **When** a matter is opened under it, **Then**
   the response says the client was not found rather than reporting a conflict.

---

### User Story 2 - A firm closes a matter, and time stops being bookable to it (Priority: P2)

A matter concludes, or a client leaves. Someone marks it closed. New time can no longer be
recorded against it, while everything already recorded stays exactly as it was and continues to
appear in reports.

**Why this priority**: This is where this feature meets the one before it. Feature 005's rule 5
reads both active flags when time is recorded, and until now nothing could change them. It is P2
because a client must exist before it can be closed.

**Independent Test**: record time against an active matter, deactivate the matter, attempt to
record more, and confirm the refusal names the matter. Then confirm the earlier entry is
untouched and still appears in the rollup.

**Acceptance Scenarios**:

1. **Given** an active matter, **When** it is deactivated, **Then** new time recorded against it
   is refused, and the refusal says the matter was inactive.
2. **Given** an active matter of a client that is then deactivated, **When** time is recorded
   against that matter, **Then** it is refused, and the refusal says the **client** was inactive.
3. **Given** a matter with recorded time, **When** it is deactivated, **Then** every existing
   entry is unchanged and still appears in the weekly rollup.
4. **Given** a deactivated client, **When** it is reactivated, **Then** time can be recorded
   against its active matters again.
5. **Given** any client or matter, **When** an update is refused, **Then** the stored record is
   unchanged.

---

### User Story 3 - Someone finds a client, its matters, or a timekeeper (Priority: P3)

A person needs the identifier of a client to bill against, wants the list of matters open under
it, or needs to know which timekeepers exist and what they bill at.

**Why this priority**: Necessary to use the system and to confirm the other two stories did what
they claimed, but it introduces no rule and cannot corrupt anything. `docs/prd.md` §7 names the
plain endpoints as the first thing to cut.

**Independent Test**: with the seeded dataset, list clients filtered by status, page through
them, list one client's matters, and fetch a timekeeper.

**Acceptance Scenarios**:

1. **Given** the seeded dataset, **When** clients are listed filtered to active only, **Then**
   no inactive client appears.
2. **Given** the same, **When** listed with no filter, **Then** both active and inactive appear
   and the result is bounded by a default page size.
3. **Given** a client with several matters, **When** its matters are requested, **Then** only
   that client's matters are returned.
4. **Given** a timekeeper identifier, **When** it is requested, **Then** the timekeeper is
   returned with the rate they currently bill at.
5. **Given** an identifier matching nothing, **When** any single record is requested, **Then**
   the response says so distinctly.

---

### Edge Cases

- **A client code that differs only by case.** `ACME` and `acme` — whether these collide is
  decided by FR-006 rather than left to whatever the storage layer happens to do.
- **A matter number reused across clients.** Must succeed. The seed deliberately does this, and
  the composite uniqueness rule exists precisely to permit it.
- **A matter opened under a client that does not exist.** Not a conflict; a missing parent.
- **Deactivating something already inactive.** Not an error — the requested state is the state.
- **Reactivating a client whose matters are all closed.** The client becomes active; the matters
  do not, because nothing said they should.
- **An update that changes nothing.** Accepted, and the record is returned unchanged.
- **Whitespace-only or empty names and codes.** Refused as malformed, before any uniqueness
  question arises.
- **A client with no matters.** Its matter list is an empty page, not an error.
- **Listing when every client is inactive.** An empty page under the active-only filter.

## Requirements *(mandatory)*

### Functional Requirements

**Registering and opening**

- **FR-001**: A client MUST be creatable with a code and a name, and MUST be active on creation.
- **FR-002**: A matter MUST be creatable under a named client with a number, a name and a default
  billable flag, and MUST be active on creation.
- **FR-003**: A newly opened matter of a newly registered client MUST immediately accept recorded
  time. A creation path that produces records the write path rejects has not delivered anything.
- **FR-004**: Creating a matter under a client that does not exist MUST report the missing client
  distinctly from a conflict — the caller's mistake is different and so is the fix.

**Uniqueness**

- **FR-005**: A client code already in use MUST be refused with a message naming the code. The
  refusal MUST be distinguishable by a caller from a malformed request and from a missing record.
- **FR-006**: Client codes MUST be compared case-insensitively for the purpose of this refusal,
  so `ACME` and `acme` are treated as the same code. A firm referring to one client by two
  spellings is a data-quality problem the API should not create.
- **FR-007**: A matter number already used **under the same client** MUST be refused naming the
  number. The same number under a different client MUST be accepted — this is a composite rule,
  and treating it as global would break the seeded dataset and the reporting it feeds.
- **FR-008**: A uniqueness collision MUST NOT surface as a raw storage failure. The schema has
  enforced both rules since feature 001; this feature makes them answerable.
- **FR-009**: The storage-level uniqueness constraints MUST remain in force. The checks added
  here are additional, not a replacement — the same defence in depth constitution P6 requires for
  the duration rules.

**Correcting and closing**

- **FR-010**: A client's name and active flag MUST be changeable. A matter's name, default
  billable flag and active flag MUST be changeable.
- **FR-011**: A client's code and a matter's number MUST be immutable after creation. The update
  request MUST NOT carry either field, so the change cannot be attempted rather than being
  attempted and refused. A caller submitting one alongside the editable fields MUST NOT have it
  silently applied.
- **FR-012**: Because of FR-011, a uniqueness collision is reachable **only on creation**. Any
  collision handling on an update path would be unreachable code, and its absence is a
  consequence of the design rather than an omission.
- **FR-013**: Deactivating a client MUST NOT change the active flag of its matters. The two flags
  are independent, and feature 005's rule 5 already reads both and reports which one refused a
  submission — a cascade would make one branch of that refusal unreachable.
- **FR-014**: Deactivation MUST NOT alter, hide or remove any recorded time entry. Entries
  against closed matters MUST continue to appear in the weekly rollup, which is the position
  feature 003 took and feature 002's seed exists to exercise.
- **FR-015**: Deactivating something already inactive MUST succeed. The caller asked for a state,
  not for a transition.
- **FR-016**: Reactivation MUST be possible for both clients and matters, by the same path that
  deactivates them.
- **FR-017**: A refused update MUST leave the stored record exactly as it was.

**Reading**

- **FR-018**: Clients MUST be listable, optionally filtered to active or inactive only, paged
  with a default bound and an upper bound.
- **FR-019**: A client's matters MUST be listable, returning only that client's matters.
- **FR-020**: A single client, matter or timekeeper MUST be retrievable by identifier, and a
  missing one MUST be reported distinctly.
- **FR-021**: Timekeepers MUST be listable and retrievable, carrying the rate they currently bill
  at. **They MUST NOT be creatable or editable through this API** — `docs/prd.md` §2.1 makes them
  seeded and read-only, and a rate that could be edited here would need a history this repository
  has ruled out.
- **FR-022**: Listing order MUST be deterministic, so paging cannot skip or repeat a record.

**Shape and access**

- **FR-023**: Codes, numbers and names MUST be rejected when empty or whitespace-only, before any
  uniqueness question is considered. A malformed request and a conflicting one are different
  answers.
- **FR-024**: Every endpoint in this feature MUST require authentication, inheriting the boundary
  established in feature 001.
- **FR-025**: Errors MUST use the same machine-readable problem format as the rest of the API.

### Key Entities

- **Client** *(existing)*: gains a write path. A code unique across the firm, a name, and an
  active flag that governs whether new time may be billed to any of its matters.
- **Matter** *(existing)*: gains a write path. Belongs to exactly one client, carries a number
  unique **within that client**, a name, a default billable flag, and its own active flag.
- **Timekeeper** *(existing, read-only)*: exposed for reading only. Carries the rate feature
  005's rule 6 captures onto each entry at the moment it is recorded.
- **Uniqueness conflict**: not stored, but a distinct result. Carries which field collided and
  the value that collided, so the caller can choose another rather than guess what went wrong.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A client and matter created through the API accept recorded time on the first
  attempt, with no manual step in between.
- **SC-002**: Two different clients can each hold a matter numbered `001`, and one client cannot
  hold two.
- **SC-003**: Every uniqueness collision returns an actionable answer naming the field and value;
  zero collisions surface as an unhandled storage failure.
- **SC-004**: Deactivating a matter refuses new time against it while leaving 100% of existing
  entries unchanged, verified by comparing the rollup's figures for that client before and after.
- **SC-005**: Deactivating a client leaves its matters' own flags untouched, verified by reading
  them back.
- **SC-006**: A refused create or update leaves stored data byte-for-byte unchanged.
- **SC-007**: Paging across a filtered client listing returns every match exactly once across at
  least three pages.
- **SC-008**: An unfiltered listing returns no more than the default page size regardless of how
  many records exist.
- **SC-009**: No timekeeper can be created or modified through any endpoint in this API.
- **SC-010**: No endpoint in this feature returns data to an unauthenticated caller.
- **SC-011**: The storage-level uniqueness constraints still reject a colliding write made
  outside the application, proving the defence in depth is real rather than claimed.

## Assumptions

- **Deactivation is the closest thing to deletion this API offers.** `docs/prd.md` §2.2 lists no
  delete endpoint for either, and time entries reference both — removing a client with recorded
  history would either orphan entries or silently destroy billing records. Closing is the domain's
  own answer and the one the schema was built for.
- **The active flag is the whole state model.** There is no draft, no pending, no archived. Two
  states, and feature 005's rules read them directly.
- **Codes and numbers are chosen by the caller**, not generated. They are how the firm refers to
  the client outside this system, so the system cannot invent them.
- **A client's code is compared case-insensitively but stored as entered.** A firm that writes
  `ACME` should see `ACME` back; a firm that then writes `acme` should be told the code is taken.
- **Any authenticated caller may create and close anything.** `docs/prd.md` §2.2 rules out RBAC;
  the token proves the caller is trusted, not who they are or what they own.
- **Timekeeper rates are read-only here and change only in the database.** Feature 005's rule 6
  test alters one directly for exactly this reason, and that remains the only way.
- **Where the uniqueness check happens is a planning decision.** That a collision must produce an
  actionable answer rather than a storage failure is the requirement; whether that is a lookup
  before the write, a caught constraint violation, or both belongs in `plan.md`.

## Out of Scope

Named so the boundary reads as chosen rather than missed.

| Not in this feature | Why |
| --- | --- |
| Any change to time entries or the six domain rules | Feature 005 shipped them. This feature changes what the rules *read*, not the rules |
| Any change to the rollup, its procedure or its index | Feature 004 measured it; a change here would put that measurement in question |
| Deleting clients or matters | `docs/prd.md` §2.2 has no such endpoint, and time entries reference both. Deactivation is the domain's answer |
| Creating or editing timekeepers | `docs/prd.md` §2.1 makes them seeded and read-only. An editable rate would need the rate history §2.2 rules out |
| Rate cards, matter-level rates, rate history | `docs/prd.md` §2.2 — one captured rate on the entry proves the point |
| Merging or renumbering clients and matters | A real operation in a real firm, and far past a demo |
| Cascading state changes of any kind | FR-013 settles the one case that arises; a general cascade mechanism is not wanted |

## Dependencies

- **Blocked by**: feature 001 for the schema and its two uniqueness constraints; feature 002 for
  the seeded records these endpoints read and page over; feature 005 for the rules whose inputs
  this feature makes writable.
- **Interacts with**: feature 005's rule 5 and feature 003's rollup. Neither is modified, but both
  are observable consequences — a matter closed here must be refused there, and entries against it
  must still be reported. Tests in this feature assert both, because a boundary is only real if
  something checks it from the other side.
- **Blocks**: nothing remaining in `docs/prd.md` §6 except the pipeline and the documentation
  artefacts, neither of which depends on this.
- **External**: none beyond what the quickstart already requires.
