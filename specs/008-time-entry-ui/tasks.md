# Tasks: Time Entry Operations UI

**Input**: Design documents from `/specs/008-time-entry-ui/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/time-entry-ui.md`, `quickstart.md`

**Tests**: The spec requires independent acceptance checks. Automated coverage stays at the ASP.NET Core host boundary; the approved plan adds no Playwright or Node test runner (R9, P13). Domain-rule pairs stay in feature 005.

**Organization**: Tasks are grouped by user story so a paged listing is a complete MVP before record, revise, delete, and refusal rendering are added.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on an incomplete task
- **[Story]**: Maps the task to User Story 1 or User Story 2
- Every task names the exact file or directory it changes or validates

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Reuse the 007 Next.js export path. Do not add a package, a host registration, or a Node step to the reviewer quickstart.

- [x] T001 Update the package description in `web/package.json` so it names both the weekly rollup and Time entries; add no runtime dependency, no Tailwind, and no component library

**Checkpoint**: The existing `web/` scaffold and `npm run build` → `src/LexTime.Api/wwwroot/` path are unchanged.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared same-origin clients, listing view-state, and shell destinations used by both stories. Reuse `web/app/token-session.ts` as-is.

**CRITICAL**: Complete this phase before either user story.

- [x] T002 [P] Implement TimeEntryDto / TimeEntryPage types, `listTimeEntries` / `getTimeEntry`, duration display as hours to one decimal, and a problem-safe error boundary in `web/app/time-entries-api.ts`; send only `from`, `to`, optional `userId` / `matterId`, `skip`, and `take` of 20, 50, or 100; never send `clientId`; do not add record/revise/delete yet
- [x] T003 [P] Implement read-only party lookups in `web/app/party-lookups.ts`: `GET /api/v1/users?take=200`, `GET /api/v1/clients?take=200`, `GET /api/v1/clients/{id}/matters?take=200`, and `GET /api/v1/matters/{id}` with an in-memory cache; include inactive parties; add no create or edit calls
- [x] T004 [P] Define exhaustive listing view states and inverted/incomplete range validation in `web/app/time-entries-state.ts` (loading, ready, empty, blocked-range, unauthenticated, unavailable) with a `never` default check
- [x] T005 Add hash destinations `#time-entries` and `#reports` (default) plus sidebar items Time entries and Reports in `web/app/page.tsx` and `web/app/globals.css`; keep the existing rollup view working; do not add Overview, Settings, or party-management destinations

**Checkpoint**: Token session, listing GET, and party reads are callable; the shell can switch destinations without a new host route.

---

## Phase 3: User Story 1 — Find recorded time (Priority: P1) MVP

**Goal**: An authenticated operator can open Time entries, filter by work-date range, timekeeper, and matter, page against the matching `total`, and read one entry's facts including captured rate.

**Independent Test**: With the seeded database and printed token, open `/#time-entries`, confirm the range is `2026-08-10`–`2026-08-13`, compare a page with `GET /api/v1/time-entries?from=2026-08-10&to=2026-08-13&skip=0&take=20`, change page size, select a row for detail, then apply the empty future window from `quickstart.md`. Reports remains reachable.

### Tests for User Story 1

> Pin the host contract first. The 401 assertion should already pass against the current API; it exists so serving HTML cannot quietly open the collection.

- [x] T006 [US1] Extend XML-documented tests in `tests/LexTime.IntegrationTests/DashboardHostTests.cs` so unauthenticated `GET /` remains 200 HTML and `GET /api/v1/time-entries?from=2026-08-10&to=2026-08-13` returns 401 without a token; keep the existing rollup 401 test; use the real Testcontainers host (P11, P25)

### Implementation for User Story 1

- [x] T007 [P] [US1] Implement the paged listing table in `web/app/time-entries-table.tsx` with work date, narrative, timekeeper, matter, duration (tenths of an hour), billable text, and captured rate; use `total` for footer copy; never invent draft/posted status or trend columns
- [x] T008 [US1] Implement Time entries filters, server-side skip/take pagination, detail pane, empty/loading/unavailable/unauthenticated states, and client-then-matter picker (client not sent on the list request) in `web/app/time-entries-view.tsx`; initial range `2026-08-10`–`2026-08-13`; page resets to 1 when filters or page size change; resolve names via `web/app/party-lookups.ts` with identifier fallback
- [x] T009 [US1] Mount `TimeEntriesView` from the `#time-entries` destination in `web/app/page.tsx` using the existing token session; preserve filters across the sign-in prompt; offer no Record/Edit/Delete controls yet
- [x] T010 [US1] Add listing, detail-pane, and filter layout in `web/app/globals.css` for desktop and tablet widths without primary horizontal scroll; billable vs not must not rely on colour alone
- [x] T011 [US1] Build `web/`, sync the export into `src/LexTime.Api/wwwroot/`, and make `DashboardHostTests` pass while confirming `/swagger`, `/health`, and existing API routes retain their prior behavior
- [x] T012 [US1] Run Validation 1 and Validation 2 from `specs/008-time-entry-ui/quickstart.md` against seeded SQL Server, compare at least one displayed row with the authenticated JSON response, and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: User Story 1 is independently usable as the listing MVP. Stop here if the one-evening cap is reached (plan P3 cut order: drop per-row matter-name fetches first).

---

## Phase 4: User Story 2 — Record, correct, and remove time (Priority: P2)

**Goal**: The operator can record, revise, and delete entries. Domain refusals show every `violations[]` detail from the service. Rate is never an input; timekeeper cannot be changed on revise.

**Independent Test**: Record 6 minutes for today against an active matter and see the captured rate; record 7 minutes and see the service increment sentence without a new row; narrative-only edit of a 2024 seed entry succeeds; changing that entry's date is refused; delete confirm/cancel behaves as in Validation 4.

### Implementation for User Story 2

- [x] T013 [P] [US2] Add `recordTimeEntry`, `reviseTimeEntry`, and `deleteTimeEntry` to `web/app/time-entries-api.ts`; POST body has no rate; PUT body has no `userId` and no rate; parse `violations[]` from a 400 problem document and preserve every element; map 404 to a missing-record error
- [x] T014 [P] [US2] Implement record / revise / delete-confirm UI in `web/app/time-entry-form.tsx`: required-field checks only (not labelled as domain rules); duration as whole minutes; work date on record defaults to today; captured rate displayed read-only after success; no timekeeper control on revise; inactive parties remain selectable and labelled
- [x] T015 [US2] Integrate the form, `violations[]` rendering, missing-record state, and post-write listing refresh into `web/app/time-entries-view.tsx`; do not re-implement increment, daily maximum, or the backdating window in the browser
- [x] T016 [US2] Complete keyboard operation, visible focus, field/error associations, and non-color cues for the form and confirm step in `web/app/globals.css` and `web/app/time-entry-form.tsx` against `specs/007-billing-operations-ui/mockups/04-time-entries.png`
- [x] T017 [US2] Rebuild and sync `web/` into `src/LexTime.Api/wwwroot/`, then run Validation 3 and Validation 4 from `specs/008-time-entry-ui/quickstart.md` and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: Both stories are independently verifiable. SC-003 refusals are visible as service text.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Close documentation, mockup-scope, generated-export, and reviewer-reproducibility obligations without expanding the product surface.

- [x] T018 [P] Update `README.md` so Time entries is reached from the same two commands, Node is only needed to regenerate `web/`, and the UI remains a thin consumer rather than the repository's primary hiring signal
- [x] T019 [P] Review `web/` against `specs/008-time-entry-ui/contracts/time-entry-ui.md` and `specs/007-billing-operations-ui/mockups/04-time-entries.png`; remove search, trend cards, realization, draft/posted status, rate inputs, timekeeper-on-revise, Settings, and working 009 navigation if any entered the implementation
- [x] T020 If `web/app/token-session.ts`, `src/LexTime.Api/Dashboard/DashboardFiles.cs`, or any SQL/auth file was touched, perform the P24 review and record it in `docs/agent-log.md`; if none were touched, record that fact in the same file instead of inventing a review
- [x] T021 Run `npm ci` and `npm run build` in `web/`, verify the committed `src/LexTime.Api/wwwroot/` exactly matches the fresh export, then run `dotnet build --warnaserror --no-incremental` followed by `dotnet test --no-build`
- [x] T022 Execute all five validations in `specs/008-time-entry-ui/quickstart.md` from a cold two-command start, including keyboard-only and tablet-width checks, and document genuine implementation friction in `docs/agent-log.md` or explicitly record that none occurred

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately
- **Foundational (Phase 2)**: Depends on Setup; blocks both stories
- **User Story 1 (Phase 3)**: Depends on Foundational; produces the MVP
- **User Story 2 (Phase 4)**: Depends on the User Story 1 listing view
- **Polish (Phase 5)**: Depends on both desired stories; T018 and T019 can begin once behavior is stable; T020 precedes any implementation commit that touched auth; T021–T022 are final gates

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on User Story 2; independently demonstrable after Phase 2
- **User Story 2 (P2)**: Builds on User Story 1's listing, but has its own write/refusal walkthrough

### Within Each User Story

- Pin the host 401 before wiring the listing into the shell
- Shared API and party modules before the view
- Table can be built in parallel with the view's first cut, then assembled
- Write helpers and the form can be built in parallel, then integrated
- Regenerate `wwwroot` after every completed UI story
- Stop at each checkpoint and validate before expanding scope

### Parallel Opportunities

- T002, T003, and T004 touch independent TypeScript files
- T007 can proceed while T006 is written (different files)
- T013 and T014 implement independent User Story 2 files
- T018 and T019 update independent documentation / review surfaces

---

## Parallel Example: User Story 1

```text
Task T007: Implement `web/app/time-entries-table.tsx`
Task T006: Extend `tests/LexTime.IntegrationTests/DashboardHostTests.cs`
```

After T002–T005 and T007, T008 assembles the listing in `web/app/time-entries-view.tsx`.

## Parallel Example: User Story 2

```text
Task T013: Add writes to `web/app/time-entries-api.ts`
Task T014: Implement `web/app/time-entry-form.tsx`
```

After both complete, T015 integrates them into `web/app/time-entries-view.tsx`.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases
2. Pin the host contract
3. Implement table, filters, paging, and detail
4. Regenerate the committed static export
5. Stop and run the User Story 1 independent test
6. Ship/demo here if the P3 evening cap is reached

### Incremental Delivery

1. Setup + Foundation → shell destinations and read clients
2. User Story 1 → paged listing MVP
3. User Story 2 → record / revise / delete and visible refusals
4. Polish → mockup audit, documentation, clean export, build/test/quickstart gates

### Scope Guard

- The mockup is a visual source, not permission to build the product
- Do not add search, KPI cards, realization, draft/posted status, Settings, or 009 screens
- Do not add a client-id listing filter, a rate field, or a timekeeper change on revise
- Do not copy the six domain rules into the browser; render `violations[]`
- No new API endpoint, schema change, CORS policy, UI test framework, or Node step in the reviewer quickstart
- P3 cut first: per-row matter-name fetches. Cut second: hash (in-memory view is enough). Never cut paging, refusal display, or Reports

---

## Notes

- `[P]` tasks operate on different files and are safe to execute concurrently
- Every C# member and test method added here requires meaningful XML documentation (P25)
- Imports stay at the top of TypeScript modules; switches over view-state unions are exhaustive
- Generated `wwwroot` is reviewer-serving output; `web/` is the editable source
- Do not restate duration increment, daily maximum, or the backdating window as client-side limits
- Commit only when explicitly requested; P17 expects spec, plan/tasks, and implementation history to remain legible
