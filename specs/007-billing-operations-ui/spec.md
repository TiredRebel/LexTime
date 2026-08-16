# Feature Specification: Billing Dashboard

**Feature Branch**: `007-billing-operations-ui`

**Created**: 2026-08-15

**Status**: Draft

**Input**: Split from the original `007-billing-operations-ui` after `/speckit-plan`
failed P3 (the spec would not fit in one evening) and, independently, P2 (`docs/prd.md`
§2.2 still lists any frontend as out of scope). This half is the application shell and
the weekly billable rollup. Recording and listing time is feature 008. Clients, matters
and timekeepers are feature 009.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand the billing workload (Priority: P1)

A billing operator opens the application and sees the reporting period being shown,
billable hours, non-billable hours, billable amount, and the clients contributing to
that period. They can change the date range and narrow the view to one client without
losing that client's standing among all clients in the period.

**Why this priority**: The weekly billable rollup is the application's headline
capability. The UI must make that existing value usable before it adds recording or
party-management convenience.

**Independent Test**: With an authenticated session and seeded data, open the dashboard,
select a known date range, filter to a known client, and compare the displayed rows and
totals with the service response. Repeat with a range that has no activity.

**Acceptance Scenarios**:

1. **Given** an authenticated user opens the application, **When** the dashboard is
   ready, **Then** the selected reporting period, each row's week identity, billable
   hours, non-billable hours, billable amount, cumulative billable hours, prior-week
   delta, and client standing are understandable without opening a separate record.
2. **Given** a valid date range, **When** the user changes the range, **Then** the
   dashboard refreshes all figures and identifies the range currently being shown.
3. **Given** a valid date range, **When** the user selects one client, **Then** only
   that client's rows are shown while its standing remains the standing among all
   clients in the selected period.
4. **Given** a valid range with no matching activity, **When** the dashboard loads,
   **Then** it shows an explicit empty state rather than stale figures or an
   error-looking blank screen.
5. **Given** a report has more rows than fit on one page, **When** the user chooses
   20, 50, or 100 rows per page and moves between pages, **Then** only that page is
   shown while the period totals and service-provided standings remain unchanged.

---

### User Story 2 - Tell empty, zero, and failure apart (Priority: P2)

The same operator hits ranges that contain only non-billable work, a first week with no
prior-week comparison, an incomplete or inverted range, a lost session, or an unavailable
service. Each of those is a different situation. None of them is presented as the others.

**Why this priority**: Window-function reports fail silently when empty, zero, and
"no comparison" are collapsed. The dashboard has to keep those distinctions visible or
it undoes the point of the rollup.

**Independent Test**: Drive the dashboard through a range with no rows, a range whose
only work is non-billable, a client's first week in the selected period (no prior-week
delta), an inverted range, an expired session, and an unavailable service. Confirm each
outcome is distinct and none shows previous figures as current.

**Acceptance Scenarios**:

1. **Given** a range that contains only non-billable work, **When** the dashboard loads,
   **Then** billable hours and amount read as zero while non-billable hours remain
   visible, and the state is not the same as "no matching data".
2. **Given** a client's first week in the selected period, **When** that row is shown,
   **Then** the prior-week change is presented as unavailable for comparison, not as
   zero.
3. **Given** an incomplete or inverted date range, **When** the user tries to apply it,
   **Then** the request is blocked with an actionable message and no previous figures
   are presented as current.
4. **Given** the session has expired, **When** the user is on the dashboard or applies
   a range, **Then** they get a clear sign-in or session-renewal action and no internal
   diagnostic text.
5. **Given** the service is unavailable, **When** a report is requested, **Then** a
   failed-request state is shown, success is not claimed, and a safe retry is offered.

---

### Edge Cases

- A session expires while a range is being applied: selected dates and client filter
  remain available after renewal so the user does not re-enter them from memory.
- A client filter matches no activity in the selected period: this is an empty success,
  not a missing-record error, and not a failure.
- A client that has since been deactivated still appears if it has activity in the
  selected period; deactivation is not implied by the report and is not offered as an
  action here.
- A range spans several weeks: week identity stays readable and rows stay grouped so a
  user can tell which week a figure belongs to.
- The network is unavailable during a read: the UI shows a loading or failed state, does
  not claim success, and allows a safe retry.
- A user operates only with a keyboard, zooms text, or uses assistive technology: focus
  order, labels, status messages, and error associations remain usable.
- A user looks for time-entry or party-management actions: none are offered. Those
  workflows belong to later features.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST provide an authenticated landing that opens the
  weekly rollup dashboard or offers a single primary path to it. It MUST NOT offer
  time-entry, client, matter, or timekeeper workflows in this feature.
- **FR-002**: The dashboard MUST display the selected date range and the existing weekly
  rollup values: week identity, billable hours, non-billable hours, billable amount,
  cumulative billable hours, prior-week delta, and client standing.
- **FR-003**: Users MUST be able to choose an inclusive start and end date, and the UI
  MUST reject an incomplete or inverted range before presenting it as current.
- **FR-004**: Users MUST be able to restrict the dashboard to one client. The UI MUST
  preserve the service-provided standing for that client among all clients in the
  selected period, not a standing computed only among the filtered rows.
- **FR-005**: The dashboard MUST distinguish loading, successful data, empty data, and
  failed requests. It MUST never show stale figures without identifying that they are
  not current.
- **FR-006**: Zero billable value MUST be distinguishable from no matching data. A
  missing prior-week comparison MUST be distinguishable from a zero change.
- **FR-007**: The UI MUST explain incomplete or inverted ranges, expired access, and
  unavailable-service outcomes in user-facing language with a safe next action, without
  exposing internal diagnostics.
- **FR-008**: The UI MUST preserve the user's selected range and client filter when a
  recoverable validation, session, or service error occurs.
- **FR-009**: The UI MUST remain usable at desktop and tablet widths and MUST NOT
  require horizontal scrolling for the dashboard at those widths.
- **FR-010**: Every interactive control MUST have an accessible name, visible focus
  state, keyboard operation, and an error or status association when applicable.
- **FR-011**: Color MUST NOT be the only signal for errors, empty versus zero, or
  report changes; equivalent text, icon, or structural cues MUST be available.
- **FR-012**: The UI MUST NOT change billing rules, report calculations, historical
  records, party ownership, or authentication requirements.
- **FR-013**: The UI MUST use the existing weekly rollup as the source of truth. It MUST
  NOT invent rows, totals, standings, or prior-week deltas locally.
- **FR-014**: The feature MUST stay inside the existing authenticated scope and MUST NOT
  introduce role-based access, multi-tenancy, offline use, or a new identity provider.
- **FR-015**: A person who can already start the existing service from the documented
  quickstart MUST be able to open the dashboard without any additional undocumented
  setup step.
- **FR-016**: The feature MUST be gated by a separate, visible amendment to
  `docs/prd.md` §2.2 that permits a thin operations UI before planning or implementation
  of this slice begins.
- **FR-017**: The rollup table MUST paginate locally with selectable page sizes of
  20, 50, and 100 rows. Pagination MUST apply after the client display filter, reset
  to the first page when the range, client, or page size changes, and MUST NOT change
  report totals, standings, or the API request.

### Technical Constraint

- **TC-001**: The front-end application MUST be implemented with Next.js and React.js.
  The planning phase may choose the supported versions, project structure, rendering
  strategy, and supporting libraries within that constraint.

### Key Entities

- **Reporting period**: The inclusive date range selected by a user for a weekly
  billable rollup. Echoed with the figures so a reader can tell what the numbers are a
  report of.
- **Weekly rollup row**: One client's figures for one week: week identity, billable and
  non-billable hours, billable amount, cumulative billable hours in the selected
  period, prior-week delta (or an explicit "no comparison"), and standing among all
  clients that week.
- **Client filter**: Either all clients in the period, or one client. Filtering changes
  which rows are shown and does not recompute standing.
- **UI session**: The current authenticated access state, the selected period and
  filter, and transient loading, empty, and error information.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a seeded-data usability walkthrough, at least 90% of users can locate
  the reporting period being shown, identify the busiest client, and explain its
  billable amount without assistance within two minutes.
- **SC-002**: 100% of the User Story 2 outcomes (empty, zero-billable, missing
  prior-week comparison, inverted range, expired session, unavailable service) show a
  distinct user-facing state with a next action; none is presented as a successful
  report of current figures when it is not.
- **SC-003**: The dashboard can be completed with keyboard input alone, and every
  interactive control has a visible focus indicator and an accessible name.
- **SC-004**: At supported desktop and tablet widths, the dashboard is usable without
  horizontal scrolling or controls becoming unreachable.
- **SC-005**: No raw stack trace, connection detail, or internal exception text is
  exposed in any acceptance scenario.
- **SC-006**: Opening the dashboard does not require any setup step that is absent from
  the documented service quickstart.
- **SC-007**: No user can reach a time-entry, client, matter, or timekeeper create or
  edit action from this feature's UI.
- **SC-008**: Before implementation is approved, the separate PRD amendment required by
  FR-016 is present, reviewed, and linked from the feature's planning artifacts.

## Assumptions

- The UI is a responsive browser experience for desktop and tablet use; a native mobile
  application is out of scope for this feature.
- Users already have a valid authenticated session supplied by the existing application
  environment. A new identity provider, account registration, or password-reset flow is
  not part of this feature; expired access is handled as a recoverable session state.
  How the existing token reaches the browser is a planning decision.
- The dashboard presents a selected inclusive range the user can change. An initial
  range, if any, is visible and correctable — never a silent default the user cannot
  see. The exact initial range is a planning decision.
- Client choice for the filter may use identities the rollup already carries (code,
  name). This feature does not include a client directory, create, or edit flow.
- The existing rollup contract remains the source of truth. This feature does not
  require new report calculations or schema changes merely to support presentation.
- Seeded data and the existing service are available for acceptance testing, including
  known billable, non-billable, empty, and multi-week ranges.
- Next.js and React.js are required; supported versions and remaining implementation
  choices belong in the planning phase.
- FR-016 is satisfied by `docs/prd.md` commit `a829911` on `main` ("Permit a thin
  operations UI now that the API is complete."). This spec remains a dashboard
  slice; it is not approval to build time-entry or party UI.

## Out of Scope

- Recording, revising, listing, or deleting time entries (feature 008).
- Creating, revising, listing, or deleting clients and matters, and any timekeeper
  directory or edit flow (feature 009).
- Changing or extending billing rules, stored historical time, report calculations, or
  party ownership.
- Role-based access control, multi-tenancy, a new identity provider, or a new
  authentication model.
- Offline use, saved report definitions, bulk editing, notifications, and native mobile
  applications.
- Implementation of this slice before its own plan and tasks exist. The PRD
  amendment landed in `a829911`; that is permission to plan, not to skip `/tasks`.

## Dependencies

- Existing authenticated access and the weekly billable rollup as already shipped.
- Seeded data with known ranges, including empty, non-billable-only, and multi-week
  activity.
- Next.js and React.js as the required front-end technology constraint.
- `docs/prd.md` §2.2 as amended in `a829911`, which permits this thin consumer and
  still excludes a product frontend.
