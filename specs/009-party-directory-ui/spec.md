# Feature Specification: Party Directory UI

**Feature Branch**: `009-party-directory-ui`

**Created**: 2026-08-17

**Status**: Draft

**Input**: Numbered as feature 009 in the 007 split. This half is the thin browser
consumer of the finished party capability: find clients, the matters that belong
to one client, and timekeepers; register a client; open a matter; correct names
and flags; close and reopen. Timekeepers remain a read-only directory. The
application shell and weekly rollup shipped in 007; time-entry operations shipped
in 008.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find clients, matters, and timekeepers (Priority: P1)

A billing operator opens Clients and sees a bounded page of the firm's clients,
including code, name, and whether each is open for new time. They restrict the
list to active or inactive, move between pages, and open one client to read it
and the matters that belong to it. They open Timekeepers and see who exists and
what they currently bill at, without any way to change those people. Reports and
Time entries remain reachable from the same shell.

**Why this priority**: The seed already has dozens of clients, hundreds of
matters, and a full timekeeper roster. Without a paged directory the operator
cannot confirm what 006 wrote, cannot pick a parent before opening a matter, and
cannot tell a closed client from an open one. Finding parties is independently
valuable even if register and close wait.

**Independent Test**: With an authenticated session and seeded data, open
Clients, apply the active-only filter, page through the matching set, and
compare the visible rows and matching total with the service listing. Open a
known client and confirm only that client's matters appear. Open Timekeepers,
read a known person's name, email, current rate, and active flag. Repeat with
filters that match nothing. Confirm Reports and Time entries remain reachable.

**Acceptance Scenarios**:

1. **Given** an authenticated user opens Clients, **When** the page is ready,
   **Then** they see the current status filter and a bounded page of clients
   showing code, name, active flag, and when the client was registered.
2. **Given** a client listing is shown, **When** the user restricts it to active
   only, to inactive only, or to both, **Then** only matching clients are shown
   and the matching total describes the filtered set, not the current page.
3. **Given** more matching clients than one page holds, **When** the user
   changes page size and moves between pages, **Then** every matching client
   appears exactly once across the pages, none is skipped, and the request
   never attempts to load the entire directory.
4. **Given** a visible client, **When** the user selects it, **Then** a detail
   view shows the same facts, and the matters belonging to that client are
   listed with number, name, default billable flag, and active flag. A client
   with no matters is an empty success, not an error.
5. **Given** a client identifier that matches nothing, **When** the operator
   tries to open it or list its matters, **Then** they see a missing-record
   state, not an unexplained failure and not a previous client's matters
   presented as current.
6. **Given** an authenticated user opens Timekeepers, **When** the page is
   ready, **Then** they see a bounded page of people showing name, email,
   current rate, and active flag. Selecting one shows the same facts and
   identifies the directory as read-only.
7. **Given** more timekeepers than one page holds, **When** the user pages,
   **Then** every timekeeper appears exactly once and the roster is never
   loaded whole.
8. **Given** filters or a client that match nothing, **When** the listing
   loads, **Then** it shows an explicit empty success, not a failed request
   and not a previous page presented as current.
9. **Given** the operator is on a party directory, **When** they choose
   Reports or Time entries, **Then** the existing weekly rollup and time-entry
   operations remain available in the same application shell.

---

### User Story 2 - Register, open, correct, and close (Priority: P2)

The same operator registers a client with a code the firm will use outside this
system, opens a first matter under it, and sees both records as active. They
correct a name, a default billable flag, or an active flag. They close a matter
or a client so new time cannot be booked there, and they can reopen one later.
When a code or matter number is already taken, the page shows the service's
conflict — naming the field and the value — and does not pretend the write
succeeded. Codes, matter numbers, and which client a matter belongs to cannot
be changed. Timekeepers cannot be created or edited.

**Why this priority**: Registration and closure are the write path the party
service already finished. The UI's job is to make that path operable and to keep
uniqueness and immutability visible, not to invent a second copy of those
rules. It is P2 because the directories have to be findable before a created or
refused write can be confirmed in the same surface.

**Independent Test**: Register one client with an unused code and open one
matter with an unused number under it. Repeat the registration with the same
code ignoring case and confirm the conflict names the code. Open a second
matter numbered `001` under a different client and confirm it succeeds; try
`001` again under the first client and confirm the conflict names the number
and that client. Rename, deactivate, and reactivate. Confirm codes, numbers,
and matter ownership cannot be edited. Confirm no create or edit action exists
for timekeepers. Confirm recorded time and the weekly rollup are unchanged
after a closure.

**Acceptance Scenarios**:

1. **Given** a code no client is using, **When** the operator registers a
   client with a name, **Then** it is saved, shown as active, and appears in
   the listing. The operator did not choose an inactive-at-create state.
2. **Given** a code another client already holds, including a spelling that
   differs only by letter case, **When** the operator registers with it,
   **Then** the write is refused, the message names the code as the conflict,
   stored data is unchanged, and success is not claimed.
3. **Given** an existing client and a number unused under that client, **When**
   the operator opens a matter with a name and a default billable flag, **Then**
   it is saved, shown as active, and listed only under that client.
4. **Given** a client that already has a matter numbered `001`, **When**
   another matter numbered `001` is opened under **that same client**, **Then**
   the write is refused naming the number; **When** a different client opens
   `001`, **Then** it succeeds.
5. **Given** a client identifier matching nothing, **When** the operator tries
   to open a matter under it, **Then** they see a missing-parent state, not a
   uniqueness conflict.
6. **Given** a register or open form, **When** a required field is missing or
   whitespace-only, **Then** the submit is blocked with an actionable field
   message and no write is attempted. That incomplete-form check is not a
   uniqueness conflict.
7. **Given** a stored client, **When** its name or active flag is changed to a
   value the service accepts, **Then** the change is saved and its code is
   unchanged. The operator is not offered a code field on correction.
8. **Given** a stored matter, **When** its name, default billable flag, or
   active flag is changed to a value the service accepts, **Then** the change
   is saved, its number is unchanged, and it still belongs to the same client.
   The operator is not offered a number field or a change of client.
9. **Given** an active matter, **When** it is deactivated, **Then** it remains
   visible as inactive, new time against it is refused by the existing
   time-entry path, and every entry already recorded is unchanged and still
   appears in the weekly rollup.
10. **Given** an active client, **When** it is deactivated, **Then** its
    matters' own flags are untouched, new time against those matters is refused
    by the existing time-entry path as a closed **client**, and recorded time
    remains in the rollup.
11. **Given** a deactivated client or matter, **When** it is reactivated,
    **Then** the requested flag is open again. Reactivating a client does not
    reopen its closed matters.
12. **Given** a timekeeper directory, **When** the operator looks for create,
    edit, rate change, or deactivate, **Then** those actions are not offered.
13. **Given** an identifier that matches no client, matter, or timekeeper,
    **When** the operator tries to open or correct it, **Then** they see a
    missing-record state, not an unexplained failure and not a blank success.

---

### Edge Cases

- A session expires while a listing or write is in flight: the operator gets a
  clear sign-in or session-renewal action, no internal diagnostic text, and the
  selected directory, client, and filters remain available after renewal.
- The service is unavailable: loading or failed state is shown, success is not
  claimed, and a safe retry is offered.
- Filters match nothing: empty success, not an error.
- A client with no matters: empty success on the matter list, not a missing
  client.
- Listing when every client is inactive: an empty page under the active-only
  filter, and both states under the unfiltered listing.
- Deactivating something already inactive, or reactivating something already
  active: accepted; the requested state is the state.
- An update that changes nothing: accepted, and the record is shown unchanged.
- A matter opened under an inactive client: the service accepts the matter if
  the parent exists; new time against it is still refused by the existing
  time-entry rules because the client is closed. The UI does not invent a
  different prohibition.
- Color is used for active versus inactive: equivalent text or structure
  remains so color is not the only signal.
- A user operates only with a keyboard, zooms text, or uses assistive
  technology: focus order, labels, status messages, and error associations
  remain usable.
- A user looks for a firm-wide matters table, search, summary count cards,
  billed-this-month figures, practice areas, roles, or recent-time widgets:
  those are not offered. Matters are always shown for one client. A path to
  Time entries MAY carry the selected matter or timekeeper as the listing
  filter that feature already supports.
- A user looks for delete, merge, or renumber: those are not offered.
  Deactivation is the close action.
- The visual mockup shows Overview, Settings, search, count cards, billed and
  unbilled amounts, roles, and recent entries: none of those are offered.
  Reports and Time entries stay in because 007 and 008 already shipped them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing application shell MUST gain Clients and Timekeepers
  destinations. Matters MUST be shown for one client at a time. The weekly
  rollup and time-entry operations MUST remain reachable. This feature MUST
  NOT offer a settings, overview, or identity-provider screen.
- **FR-002**: The client listing MUST show each client's code, name, active
  flag, and registration time. The UI MUST NOT invent matter counts, billed
  amounts, last-activity figures, or other facts the client listing does not
  carry.
- **FR-003**: Users MUST be able to restrict the client listing to active,
  inactive, or both, matching the status filter the existing listing already
  supports. The UI MUST NOT present a search box or any other listing filter
  that listing does not support.
- **FR-004**: Client, matter, and timekeeper listings MUST paginate against
  the matching total with a bounded page size. Changing filters or page size
  MUST return to the first page of the new matching set. The UI MUST NOT load
  or request an entire directory.
- **FR-005**: The UI MUST distinguish loading, successful data, empty data,
  missing records, and failed requests. It MUST never show a previous page or
  a previous write result as current without identifying that it is not.
- **FR-006**: Users MUST be able to open one client and list only that
  client's matters, showing number, name, default billable flag, and active
  flag. A client with no matters MUST be an empty success. A missing client
  MUST be a missing-record state, not an empty matter list.
- **FR-007**: There MUST NOT be a firm-wide matters listing. The UI MUST NOT
  invent practice-area, responsible-timekeeper, or other matter filters the
  existing matter listing does not support.
- **FR-008**: Users MUST be able to list and open timekeepers, showing name,
  email, current rate, and active flag. The UI MUST NOT invent roles, partner
  or associate counts, recent-time feeds, or billed-hour summaries.
- **FR-009**: Users MUST be able to register a client by supplying a code and
  a name. A newly registered client MUST appear as active. The operator MUST
  NOT set inactive-at-create.
- **FR-010**: Users MUST be able to open a matter under a named existing
  client by supplying a number, a name, and a default billable flag. A newly
  opened matter MUST appear as active under that client only.
- **FR-011**: The UI MUST present every uniqueness conflict the service
  returns, in user-facing language that names the field and the colliding
  value, without dropping the case-insensitive client-code rule and without
  treating a matter number reused under a different client as a conflict.
  Incomplete or whitespace-only required fields MAY be blocked locally; that
  check MUST NOT be presented as a uniqueness conflict.
- **FR-012**: Opening a matter under a missing client MUST produce a
  missing-parent state, distinct from a uniqueness conflict and from a
  malformed form.
- **FR-013**: Users MUST be able to correct a client's name and active flag,
  and a matter's name, default billable flag, and active flag, subject to the
  service's existing update rules.
- **FR-014**: A client's code and a matter's number MUST NOT be editable after
  creation. The correction UI MUST NOT offer those fields. Moving a matter to
  a different client MUST NOT be offered.
- **FR-015**: Deactivating a client MUST NOT change its matters' active flags.
  Deactivating a client or matter MUST NOT hide, alter, or remove recorded
  time. Reactivation MUST be possible by the same path, and reactivating a
  client MUST NOT reopen its closed matters.
- **FR-016**: The UI MUST NOT offer create, edit, rate change, or deactivate
  for timekeepers. The directory is read-only.
- **FR-017**: The UI MUST NOT offer deletion of clients or matters.
  Deactivation is the close action.
- **FR-018**: Opening or correcting an identifier that matches no record MUST
  produce a missing-record state, not an unexplained failure and not a silent
  success.
- **FR-019**: The UI MUST explain expired access and unavailable-service
  outcomes in user-facing language with a safe next action, without exposing
  internal diagnostics.
- **FR-020**: The UI MUST preserve the user's selected directory, client, and
  listing filters when a recoverable validation, session, or service error
  occurs.
- **FR-021**: The UI MUST remain usable at desktop and tablet widths and MUST
  NOT require horizontal scrolling for the listings at those widths.
- **FR-022**: Every interactive control MUST have an accessible name, visible
  focus state, keyboard operation, and an error or status association when
  applicable.
- **FR-023**: Color MUST NOT be the only signal for errors, empty versus data,
  or active versus inactive; equivalent text, icon, or structural cues MUST be
  available.
- **FR-024**: The UI MUST NOT change billing rules, stored historical time,
  report calculations, party uniqueness, code or number immutability, or
  authentication requirements. It MUST NOT invent listing totals, billed or
  unbilled amounts, search, roles, or summary count cards.
- **FR-025**: The feature MUST stay inside the existing authenticated scope
  and MUST NOT introduce role-based access, multi-tenancy, offline use, or a
  new identity provider. It reuses the existing session established for the
  dashboard.
- **FR-026**: A person who can already start the existing service from the
  documented quickstart MUST be able to open the party directories without any
  additional undocumented setup step.
- **FR-027**: A path from a selected matter or timekeeper to Time entries MAY
  reuse that feature's existing listing filters. That path MUST NOT invent a
  recent-entries widget or figures Time entries does not already show.

### Technical Constraint

- **TC-001**: The front-end application MUST continue to use Next.js and
  React.js, as already required for the dashboard shell. The planning phase
  may choose versions, project structure, and supporting libraries within that
  inherited constraint.

### Key Entities

- **Client**: A billed party with a firm-wide code, a name, and an active flag
  that governs whether new time may be booked to any of its matters. The code
  is chosen at registration and does not change.
- **Matter**: Work opened under exactly one client, with a number unique
  within that client, a name, a default billable flag for new entries, and its
  own active flag. The number and the owning client do not change after
  opening.
- **Timekeeper**: A person who records time. Shown with name, email, current
  rate, and active flag. Created only by the seed; not editable here.
- **Uniqueness conflict**: A service-provided explanation that a registration
  or opening collided with an existing code or matter number. It names the
  field and the value. The UI displays it; it does not author it.
- **UI session**: The current authenticated access state, the selected
  directory and client, listing filters, the selected record if any, and
  transient loading, empty, conflict, and error information.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a seeded-data usability walkthrough, at least 90% of users can
  locate a known client by code, open it, and see only that client's matters,
  without assistance within two minutes.
- **SC-002**: In the same walkthrough, at least 90% of users can register one
  client with an unused code and open one matter under it, without assistance,
  within two minutes of opening the register action.
- **SC-003**: 100% of driven uniqueness conflicts (a taken client code, a
  taken code that differs only by case, and a matter number reused under the
  same client) show the service's reason naming the field and value, leave
  stored data unchanged, and do not claim success. The same matter number
  under two different clients succeeds.
- **SC-004**: 100% of empty-listing, missing-record, missing-parent,
  expired-session, and unavailable-service outcomes show a distinct
  user-facing state with a next action; none is presented as a successful
  current listing or write.
- **SC-005**: After a matter or client is deactivated, 100% of previously
  recorded time for that party remains visible in the weekly rollup, and new
  time against it is refused by the existing time-entry path.
- **SC-006**: Party directories can be listed, opened, registered, corrected,
  and closed with keyboard input alone, and every interactive control has a
  visible focus indicator and an accessible name.
- **SC-007**: At supported desktop and tablet widths, the listings and
  register or correct flows are usable without horizontal scrolling or
  controls becoming unreachable.
- **SC-008**: No raw stack trace, connection detail, or internal exception
  text is exposed in any acceptance scenario.
- **SC-009**: Opening the party directories does not require any setup step
  that is absent from the documented service quickstart.
- **SC-010**: No user can edit a client code, edit a matter number, move a
  matter between clients, delete a client or matter, or reach a timekeeper
  create or edit action from this feature's UI.
- **SC-011**: No user can reach search, summary count cards, billed or
  unbilled amounts, roles, practice areas, a firm-wide matters table, or
  Settings from this feature's UI.

## Assumptions

- The UI is a responsive browser experience for desktop and tablet use; a
  native mobile application is out of scope.
- Users already have a valid authenticated session from the existing
  dashboard. A new identity provider, account registration, or password-reset
  flow is not part of this feature; expired access is handled as a recoverable
  session state.
- The existing party contract remains the source of truth, including which
  filters each listing supports, which fields may be registered or corrected,
  that codes and numbers are immutable, that matters are listed per client,
  and that timekeepers are read-only. This feature does not add billing
  behaviour or schema merely to support presentation.
- How the operator reaches a client-scoped matter list — from the client
  record, or by first choosing a client on a Matters destination — is a
  planning decision. Either way, matters are never listed across the firm.
- Page size is bounded and selectable. Exact sizes are a planning decision;
  they MUST stay within what each listing already permits.
- An initial client-status filter, if any, is visible and correctable — never
  a silent default the user cannot see. The exact initial filter is a planning
  decision. Timekeeper and matter listings have no status query on the
  existing contract; the UI MUST NOT pretend they do.
- Seeded data and the existing service are available for acceptance testing,
  including known clients, inactive parties, colliding codes and numbers, and
  clients with no matters.
- Next.js and React.js remain required because they already ship the shell;
  versions and remaining implementation choices belong in the planning phase.
- The PRD already permits a thin operations UI (`docs/prd.md` commit
  `a829911`). This spec is the party-directory slice of that permission; it is
  not approval to build a product frontend.
- This slice is sized to one evening because it consumes finished capability
  and adds no billing rules. If planning finds three directories will not fit,
  timekeepers (read-only) MUST be split before `/speckit-tasks` rather than
  quietly overrunning.

## Out of Scope

- Recording, revising, listing, or deleting time entries (feature 008). A
  navigation path that reuses 008's existing matter or timekeeper filter is
  allowed; a recent-entries widget is not.
- Changing or extending the six domain rules, the captured-rate rule, stored
  historical time, report calculations, uniqueness rules, or code and number
  immutability.
- Creating, editing, or deleting timekeepers, or changing a current rate.
- Deleting, merging, or renumbering clients and matters.
- A firm-wide matters listing, a search box, summary count cards, billed or
  unbilled amounts, practice areas, roles, or last-activity figures.
- Role-based access control, multi-tenancy, a new identity provider, or a new
  authentication model.
- Settings, theme, profile, Overview, and export screens from the visual
  mockup.
- Implementation of this slice before its own plan and tasks exist.

## Dependencies

- Feature 007's application shell, session, and weekly rollup, which remain
  reachable.
- Feature 008's time-entry operations, which remain reachable and which
  consume the directories this feature writes.
- Feature 006's finished client, matter, and timekeeper directories, including
  uniqueness refusals, immutability, and the absence of timekeeper writes.
- Seeded data large enough that paging is mandatory, including inactive
  parties, colliding codes, and matter numbers reused across clients.
- Next.js and React.js as the inherited front-end technology constraint.
- `docs/prd.md` §2.2 as amended in `a829911`, which permits this thin consumer
  and still excludes a product frontend.

## Visual source

The intended chrome and tables are

- [`specs/007-billing-operations-ui/mockups/05-clients.png`](../007-billing-operations-ui/mockups/05-clients.png)
  — Clients list and detail
- [`specs/007-billing-operations-ui/mockups/06-matters.png`](../007-billing-operations-ui/mockups/06-matters.png)
  — Matters list and detail, always under one client
- [`specs/007-billing-operations-ui/mockups/07-timekeepers.png`](../007-billing-operations-ui/mockups/07-timekeepers.png)
  — Timekeepers, marked read-only

Those mockups also show product chrome this spec refuses: Overview and Settings
destinations, search, summary count cards, billed and unbilled amounts, roles,
practice areas, responsible-timekeeper filters, and recent-time widgets. Those
stay out. Reports stays in because 007 already shipped it. Time entries stays
in because 008 already shipped it.
