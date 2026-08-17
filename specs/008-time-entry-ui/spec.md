# Feature Specification: Time Entry Operations UI

**Feature Branch**: `008-time-entry-ui`

**Created**: 2026-08-16

**Status**: Draft

**Input**: Numbered as feature 008 in the 007 split. This half is the thin browser
consumer of the finished time-entry capability: find, record, revise, and delete
entries, and show the service-provided domain-rule refusals. Clients, matters,
and timekeepers as managed directories remain feature 009. The application shell
and weekly billable rollup already shipped in 007.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find recorded time (Priority: P1)

A billing operator opens Time entries and sees a bounded page of work already
recorded. They narrow the view by work-date range, timekeeper, and matter, move
between pages of the matching set, and open one entry to read its narrative,
duration, billable flag, captured rate, and identities without leaving the page.

**Why this priority**: The seeded dataset is hundreds of thousands of entries.
Without a filtered, paged reading surface the write path cannot be verified in
the browser and the operator cannot confirm what is already on a day before
recording more. Finding time is independently valuable even if recording waits.

**Independent Test**: With an authenticated session and seeded data, open Time
entries, apply a known date range plus a known timekeeper and matter, page
through the result, and compare the visible rows and the matching total with the
service listing. Repeat with a filter that matches nothing. Confirm the weekly
rollup remains reachable from the same shell.

**Acceptance Scenarios**:

1. **Given** an authenticated user opens Time entries, **When** the page is
   ready, **Then** they see the current filters and a bounded page of entries
   showing work date, narrative, matter identity, timekeeper identity, duration,
   billable flag, and captured rate.
2. **Given** a listing is shown, **When** the user sets an inclusive work-date
   range, one timekeeper, one matter, or any combination, **Then** only matching
   entries are shown and the matching total describes the filtered set, not the
   current page.
3. **Given** more matching entries than one page holds, **When** the user
   changes page size and moves between pages, **Then** every matching entry
   appears exactly once across the pages, none is skipped, and the request never
   attempts to load the entire table.
4. **Given** a visible entry, **When** the user selects it, **Then** a detail
   view shows the same facts plus when it was recorded and last revised, without
   inventing a draft/posted status or other workflow the service does not have.
5. **Given** filters that match nothing, **When** the listing loads, **Then** it
   shows an explicit empty success, not a failed request and not a previous
   page presented as current.
6. **Given** the operator is on Time entries, **When** they choose Reports,
   **Then** the existing weekly rollup remains available in the same application
   shell.

---

### User Story 2 - Record, correct, and remove time (Priority: P2)

The same operator records six minutes against an active matter, sees the
captured rate appear without typing it, and finds the new row in the listing.
They correct a narrative or duration, and they remove an entry they recorded by
mistake. When a submission would break a domain rule, the page shows the
service's reason — naming the value and the rule — and does not pretend the
write succeeded.

**Why this priority**: Recording is the write path the API already finished.
The UI's job is to make that path operable and to keep the six rules visible
when they refuse, not to invent a second copy of them. It is P2 because finding
time has to work before a recorded or refused write can be confirmed in the
same surface.

**Independent Test**: Record one conforming entry and confirm it appears with a
captured rate the operator did not type. Drive one refusing case per domain
rule the service can refuse on create or revise, plus a narrative-only correction
of an old entry and a date change of an old entry. Delete one entry after
confirmation and confirm it is gone. Confirm there is no rate field and no
timekeeper change on revise.

**Acceptance Scenarios**:

1. **Given** an active timekeeper, an active matter of an active client, and a
   valid duration and work date, **When** the operator records the entry,
   **Then** it is saved, returned in the listing, and shown with the rate
   captured from the timekeeper — a rate the operator did not supply.
2. **Given** a recording or revision form, **When** a required field is missing,
   **Then** the submit is blocked with an actionable field message and no write
   is attempted. That incomplete-form check is not a domain rule.
3. **Given** a submission the service refuses, **When** the operator records or
   revises, **Then** every returned refusal is shown in user-facing language
   that names the offending value and the rule, the stored data is unchanged,
   and success is not claimed.
4. **Given** a recorded entry, **When** its matter, work date, duration,
   billable flag, or narrative is changed to a value the service accepts,
   **Then** the change is saved and the captured rate remains the stored rate.
5. **Given** a recorded entry, **When** the operator tries to change which
   timekeeper owns it, **Then** that action is not offered. Moving time between
   people is a new recording, not a revision.
6. **Given** an entry whose work date is now outside the backdating window,
   **When** only the narrative is corrected, **Then** the correction is saved;
   **When** the work date is moved, **Then** the service refusal is shown and
   the stored date is unchanged.
7. **Given** a recorded entry, **When** the operator confirms deletion,
   **Then** it no longer appears in the listing. Cancelling the confirmation
   leaves it in place.
8. **Given** an identifier that matches no entry, **When** the operator tries to
   open, revise, or delete it, **Then** they see a missing-record state, not an
   unexplained failure and not a blank success.

---

### Edge Cases

- A session expires while a listing or write is in flight: the operator gets a
  clear sign-in or session-renewal action, no internal diagnostic text, and the
  selected filters remain available after renewal.
- The service is unavailable: loading or failed state is shown, success is not
  claimed, and a safe retry is offered.
- An incomplete or inverted work-date range is blocked with an actionable
  message and does not present a previous page as current.
- A duration that is not a positive multiple of six minutes, a single entry
  above one day, or a day that would exceed the timekeeper's daily maximum: the
  service refusal is shown; the UI does not invent a different limit.
- A work date of tomorrow, or older than the backdating window: refused on
  record, and on revise only when the date is actually being changed.
- An inactive matter, an active matter whose client is inactive, or an inactive
  timekeeper: the service refusal is shown, including which party was inactive
  when the service says so.
- Several rules fail on one submission: every returned refusal is shown, not
  only the first.
- Filters match nothing: empty success, not an error.
- The seeded table is far larger than one page: the UI pages through the
  matching total and never treats "all entries" as a loadable result.
- Color is used for billable versus not: equivalent text or structure remains
  so color is not the only signal.
- A user operates only with a keyboard, zooms text, or uses assistive
  technology: focus order, labels, status messages, and error associations
  remain usable.
- A user looks for client, matter, or timekeeper create or edit screens: those
  are not offered. Choosing an existing timekeeper or matter to record against
  is not party management.
- The visual mockup shows search, period-over-period summary cards,
  realization, and draft/posted status: none of those are offered. They are
  product chrome or invented figures the listing does not supply.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing application shell MUST gain a Time entries
  destination. The weekly rollup MUST remain reachable. This feature MUST NOT
  offer client, matter, or timekeeper create or edit workflows, and MUST NOT
  offer a settings or identity-provider screen.
- **FR-002**: The listing MUST show each entry's work date, narrative, matter
  identity, timekeeper identity, duration, billable flag, and captured rate.
  Display names MAY be resolved from the existing party directories. The UI
  MUST NOT invent client, matter, or timekeeper records to do so.
- **FR-003**: Users MUST be able to restrict the listing by inclusive work-date
  range, by one timekeeper, and by one matter, each independently optional. The
  UI MUST NOT present a client-id listing filter the existing listing does not
  support.
- **FR-004**: The listing MUST paginate against the matching total with a
  bounded page size. Changing filters or page size MUST return to the first
  page of the new matching set. The UI MUST NOT load or request the entire
  table.
- **FR-005**: The UI MUST distinguish loading, successful data, empty data, and
  failed requests. It MUST never show a previous page or a previous write
  result as current without identifying that it is not.
- **FR-006**: Users MUST be able to record an entry by choosing an existing
  timekeeper and matter, a work date, a duration, a billable flag, and a
  narrative.
- **FR-007**: The operator MUST NOT be able to supply or edit the hourly rate.
  After a successful record, the captured rate MUST be visible. After a
  successful revise, the captured rate MUST remain the stored value.
- **FR-008**: Revising an entry MUST NOT offer a change of timekeeper. Moving
  work between people is a new recording.
- **FR-009**: Users MUST be able to revise an entry's matter, work date,
  duration, billable flag, and narrative, subject to the service's existing
  update rules.
- **FR-010**: The UI MUST present every domain-rule refusal the service
  returns, in user-facing language, without dropping extras and without
  substituting a different limit, message, or threshold. Incomplete required
  fields MAY be blocked locally; that check MUST NOT be presented as one of
  the domain rules.
- **FR-011**: Users MUST be able to delete an entry after an explicit
  confirmation. Cancelling MUST leave the entry in place.
- **FR-012**: Opening, revising, or deleting an identifier that matches no
  entry MUST produce a missing-record state, not an unexplained failure and
  not a silent success.
- **FR-013**: The UI MUST explain expired access and unavailable-service
  outcomes in user-facing language with a safe next action, without exposing
  internal diagnostics.
- **FR-014**: The UI MUST preserve the user's listing filters when a
  recoverable validation, session, or service error occurs.
- **FR-015**: The UI MUST remain usable at desktop and tablet widths and MUST
  NOT require horizontal scrolling for the listing at those widths.
- **FR-016**: Every interactive control MUST have an accessible name, visible
  focus state, keyboard operation, and an error or status association when
  applicable.
- **FR-017**: Color MUST NOT be the only signal for errors, empty versus data,
  or billable versus not; equivalent text, icon, or structural cues MUST be
  available.
- **FR-018**: The UI MUST NOT change billing rules, stored historical time,
  report calculations, party ownership, or authentication requirements. It
  MUST NOT invent listing totals, period-over-period trends, realization
  figures, free-text search, or draft/posted status.
- **FR-019**: The feature MUST stay inside the existing authenticated scope
  and MUST NOT introduce role-based access, multi-tenancy, offline use, or a
  new identity provider. It reuses the existing session established for the
  dashboard.
- **FR-020**: A person who can already start the existing service from the
  documented quickstart MUST be able to open Time entries without any
  additional undocumented setup step.
- **FR-021**: Choosing a timekeeper or matter MUST use the existing
  directories as read-only sources. Creating, renaming, activating, or
  deactivating those parties is out of this feature.

### Technical Constraint

- **TC-001**: The front-end application MUST continue to use Next.js and
  React.js, as already required for the dashboard shell. The planning phase
  may choose versions, project structure, and supporting libraries within that
  inherited constraint.

### Key Entities

- **Time entry**: A block of work recorded by one timekeeper against one
  matter on one work date: duration, billable flag, narrative, and a captured
  hourly rate that does not change after recording.
- **Listing filters**: The operator's current inclusive work-date range,
  optional timekeeper, optional matter, page window, and matching total. The
  total is how many entries match the filters, not how many are on the page.
- **Domain refusal**: A service-provided explanation that a record or revise
  broke one of the existing billing rules. It names the value and the rule.
  The UI displays it; it does not author it.
- **UI session**: The current authenticated access state, the selected
  filters, the selected entry if any, and transient loading, empty, refusal,
  and error information.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a seeded-data usability walkthrough, at least 90% of users can
  locate a known entry by work date, timekeeper, and matter, and read its
  duration and captured rate, without assistance within two minutes.
- **SC-002**: In the same walkthrough, at least 90% of users can record one
  conforming entry and see it in the listing, without assistance, within two
  minutes of opening the record action.
- **SC-003**: 100% of driven domain-rule refusals (invalid duration increment,
  single-entry over one day, daily maximum, future date, backdating window,
  inactive matter or client, inactive timekeeper, and a date change on an
  entry now outside the window) show the service's reason and do not claim
  success. A narrative-only correction of an old entry succeeds.
- **SC-004**: 100% of empty-listing, missing-record, expired-session, and
  unavailable-service outcomes show a distinct user-facing state with a next
  action; none is presented as a successful current listing or write.
- **SC-005**: Time entries can be listed, recorded, revised, and deleted with
  keyboard input alone, and every interactive control has a visible focus
  indicator and an accessible name.
- **SC-006**: At supported desktop and tablet widths, the listing and record
  or revise flow are usable without horizontal scrolling or controls becoming
  unreachable.
- **SC-007**: No raw stack trace, connection detail, or internal exception
  text is exposed in any acceptance scenario.
- **SC-008**: Opening Time entries does not require any setup step that is
  absent from the documented service quickstart.
- **SC-009**: No user can supply a rate, change an entry's timekeeper, or
  reach a client, matter, or timekeeper create or edit action from this
  feature's UI.
- **SC-010**: No user can reach search, period-over-period summary cards,
  realization, or draft/posted status from this feature's UI.

## Assumptions

- The UI is a responsive browser experience for desktop and tablet use; a
  native mobile application is out of scope.
- Users already have a valid authenticated session from the existing
  dashboard. A new identity provider, account registration, or password-reset
  flow is not part of this feature; expired access is handled as a recoverable
  session state.
- Timekeeper and matter choice uses identities the existing directories
  already carry. This feature does not include a party directory, create, or
  edit flow.
- The existing time-entry contract remains the source of truth, including
  which filters the listing supports, which fields may be recorded or revised,
  and which rules apply on update. This feature does not add billing behaviour
  or schema merely to support presentation.
- Display names for timekeepers and matters may be resolved from those
  directories. If a name cannot be resolved, the entry remains identifiable by
  the identities the listing already carries.
- Duration is shown in a form a billing operator can reconcile with tenths of
  an hour. The exact presentation (minutes, hours, or both) is a planning
  decision and MUST remain consistent with the stored duration.
- Page size is bounded and selectable. Exact sizes are a planning decision;
  they MUST stay within what the listing already permits.
- An initial filter, if any, is visible and correctable — never a silent
  default the user cannot see. The exact initial range is a planning decision.
- Seeded data and the existing service are available for acceptance testing,
  including known conforming entries, empty filters, inactive parties, and
  entries whose work dates now sit outside the backdating window.
- Next.js and React.js remain required because they already ship the shell;
  versions and remaining implementation choices belong in the planning phase.
- The PRD already permits a thin operations UI (`docs/prd.md` commit
  `a829911`). This spec is the time-entry slice of that permission; it is not
  approval to build party-management UI.
- This slice is sized to one evening because it consumes finished capability
  and adds no billing rules. If planning finds it will not fit, it MUST be
  split before `/speckit-tasks` rather than quietly overrunning.

## Out of Scope

- Creating, revising, listing-as-directory, or deleting clients, matters, or
  timekeepers (feature 009). Read-only pickers and display names are in scope.
- Changing or extending the six domain rules, the captured-rate rule, stored
  historical time, report calculations, or party ownership.
- Role-based access control, multi-tenancy, a new identity provider, or a new
  authentication model.
- Free-text search, period-over-period summary cards, realization, export,
  bulk edit, notifications, offline use, and native mobile applications.
- Draft, posted, locked, or any other time-entry workflow status. Deletion
  remains unrestricted after confirmation, matching the existing service.
- A client-id filter on the listing, a rate input, or a timekeeper change on
  revise.
- Settings, theme, and profile screens from the visual mockup.
- Implementation of this slice before its own plan and tasks exist.

## Dependencies

- Feature 007's application shell, session, and weekly rollup, which remain
  reachable.
- Feature 005's finished time-entry listing, record, revise, delete, and
  domain-rule refusals.
- Feature 006's existing client, matter, and timekeeper directories, consumed
  read-only for pickers and display names.
- Seeded data large enough that paging is mandatory, including inactive
  parties and history outside the backdating window.
- Next.js and React.js as the inherited front-end technology constraint.
- `docs/prd.md` §2.2 as amended in `a829911`, which permits this thin consumer
  and still excludes a product frontend.

## Visual source

The intended layout is
[`specs/007-billing-operations-ui/mockups/04-time-entries.png`](../007-billing-operations-ui/mockups/04-time-entries.png):
navy shell, Time entries as the current destination, date / timekeeper /
matter filters, a paged table, a detail pane, Record time, and Edit entry.

That mockup also shows product chrome this spec refuses: Overview and Settings
destinations, client/matter/timekeeper management destinations, a search box,
four trend cards, realization, and draft/posted status. Those stay out. Reports
stays in because 007 already shipped it.
