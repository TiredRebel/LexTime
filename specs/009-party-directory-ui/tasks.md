# Tasks: Party Directory UI

**Input**: Design documents from `/specs/009-party-directory-ui/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/party-directory-ui.md`, `quickstart.md`

**Tests**: The spec requires independent acceptance checks. Automated coverage stays at the ASP.NET Core host boundary; the approved plan adds no Playwright or Node test runner (R8, P13). Uniqueness, immutability, and timekeeper-unwritable pairs stay in feature 006.

**Organization**: Tasks are grouped by user story so paged Clients (with nested matters) and read-only Timekeepers are a complete MVP before register, open, correct, close, and 409 rendering are added.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on an incomplete task
- **[Story]**: Maps the task to User Story 1 or User Story 2
- Every task names the exact file or directory it changes or validates

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Reuse the 007 Next.js export path. Do not add a package, a host registration, or a Node step to the reviewer quickstart.

- [x] T001 Update the package description in `web/package.json` so it names the weekly rollup, Time entries, and party directories; add no runtime dependency, no Tailwind, and no component library

**Checkpoint**: The existing `web/` scaffold and `npm run build` → `src/LexTime.Api/wwwroot/` path are unchanged. Do not rewrite `web/app/party-lookups.ts`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared same-origin directory client, listing view-state, and shell destinations used by both stories. Reuse `web/app/token-session.ts` as-is. Leave `web/app/party-lookups.ts` on `take=200` for 008 pickers (R10).

**CRITICAL**: Complete this phase before either user story.

- [x] T002 [P] Implement ClientDto / MatterDto / TimekeeperDto / page envelopes, paged `listClients` / `getClient` / `listMattersForClient` / `listTimekeepers` / `getTimekeeper`, and a problem-safe `PartyRequestError` in `web/app/parties-api.ts`; send `skip` and `take` of 20, 50, or 100; send `isActive` only on the client list and only when the filter is not All; never send a search term or a firm-wide matters request; do not add register/correct/open yet
- [x] T003 [P] Define exhaustive listing view states in `web/app/parties-state.ts` (loading, ready, empty, missing, unauthenticated, unavailable) with a `never` default check
- [x] T004 Add hash destinations `#clients` and `#timekeepers` while keeping `#time-entries` and `#reports` (default) plus sidebar items Clients, Timekeepers, Time entries, and Reports in `web/app/page.tsx` and `web/app/globals.css`; keep the existing rollup and time-entry views working; do not add Overview, Settings, or a Matters destination

**Checkpoint**: Token session and directory GETs are callable; the shell can switch destinations without a new host route.

---

## Phase 3: User Story 1 — Find clients, matters, and timekeepers (Priority: P1) MVP

**Goal**: An authenticated operator can open Clients, filter by status, page against the matching `total`, open one client and see only that client's matters, and open Timekeepers as a read-only roster. Reports and Time entries remain reachable.

**Independent Test**: With the seeded database and printed token, open `/#clients`, confirm the status filter is All, compare a page with `GET /api/v1/clients?skip=0&take=20`, restrict to Inactive, select a client and confirm nested matters match `GET /api/v1/clients/{id}/matters?skip=0&take=20`, open `/#timekeepers` and compare with `GET /api/v1/users?skip=0&take=20`. There is no search box, no count cards, and no Matters sidebar item. Reports and Time entries remain reachable.

### Tests for User Story 1

> Pin the host contract first. The 401 assertions should already pass against the current API; they exist so serving HTML cannot quietly open the directories.

- [x] T005 [US1] Extend XML-documented tests in `tests/LexTime.IntegrationTests/DashboardHostTests.cs` so unauthenticated `GET /` remains 200 HTML and both `GET /api/v1/clients` and `GET /api/v1/users` return 401 without a token; keep the existing rollup and time-entry 401 tests; use the real Testcontainers host (P11, P25)

### Implementation for User Story 1

- [x] T006 [P] [US1] Implement the paged client table in `web/app/clients-table.tsx` with code, name, active flag as text, and registration time; use `total` for footer copy; never invent matter counts, billed amounts, or last-activity columns
- [x] T007 [P] [US1] Implement the paged Timekeepers roster and read-only detail pane in `web/app/timekeepers-view.tsx` with name, email, current rate, and active flag; label the pane read-only; page against `total`; add no create, edit, rate, or deactivate control
- [x] T008 [US1] Implement Clients status filter (All / Active / Inactive), server-side skip/take pagination, client detail, nested matter table (number, name, default billable, active), empty/loading/unavailable/unauthenticated/missing states, and missing-parent vs empty-matters distinction in `web/app/clients-view.tsx`; initial filter All; page resets to 1 when filter, page size, or selected client (for matters) changes; offer no register/correct/open controls yet
- [x] T009 [US1] Mount `ClientsView` from `#clients` and `TimekeepersView` from `#timekeepers` in `web/app/page.tsx` using the existing token session; preserve destination, client, and filters across the sign-in prompt
- [x] T010 [US1] Add directory, nested-matter, and detail-pane layout in `web/app/globals.css` for desktop and tablet widths without primary horizontal scroll; active vs inactive must not rely on colour alone
- [x] T011 [US1] Build `web/`, sync the export into `src/LexTime.Api/wwwroot/`, and make `DashboardHostTests` pass while confirming `/swagger`, `/health`, and existing API routes retain their prior behavior
- [x] T012 [US1] Run Validation 1 and Validation 2 from `specs/009-party-directory-ui/quickstart.md` against seeded SQL Server, compare at least one displayed client and matter row with the authenticated JSON response, and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: User Story 1 is independently usable as the directory MVP. Stop here if the one-evening cap is reached (plan P3 cut order: drop the Time entries deep-link first — it is not a task in this file; then skip putting the selected client in the hash). If the evening still overruns, stop and split timekeepers into 010 rather than shipping Clients without Timekeepers.

---

## Phase 4: User Story 2 — Register, open, correct, and close (Priority: P2)

**Goal**: The operator can register a client, open a matter under the selected client, correct names and flags, and close or reopen. Uniqueness conflicts show the service `conflictingField` and `conflictingValue`. Codes, numbers, and matter ownership are not offered as inputs on correct. Timekeepers remain unwritable.

**Independent Test**: Register unused code `WALK`; collide with `WALK` and with `walk`; open matter `001` under the new client; collide `001` on that client and succeed with `001` on a different client; rename; deactivate a client and confirm nested matter flags are unchanged; deactivate a seeded client with rollup rows and confirm Reports figures do not vanish; confirm Timekeepers still has no write controls (Validation 3 and Validation 4).

### Implementation for User Story 2

- [x] T013 [P] [US2] Add `registerClient`, `correctClient`, `openMatter`, and `correctMatter` to `web/app/parties-api.ts`; POST client has no `isActive`; PUT client has no `clientCode`; POST matter has no `clientId` in the body and no `isActive`; PUT matter has no `matterNumber` and no `clientId`; parse `conflictingField` / `conflictingValue` from a 409 problem document; map 404 on open-matter to missing-parent and 404 on get/correct to missing-record; add no timekeeper write helper
- [x] T014 [P] [US2] Implement register / correct-client UI in `web/app/client-form.tsx`: required-field checks only (not labelled as uniqueness conflicts); code input on register only; code shown as read-only text on correct; no inactive-at-create control; no delete control
- [x] T015 [P] [US2] Implement open / correct-matter UI in `web/app/matter-form.tsx`: required-field checks only; number input on open only; number and owning client shown as read-only text on correct; default billable and active on correct; no delete control; no change-of-client control
- [x] T016 [US2] Integrate the forms, 409 field-and-value rendering, missing-record and missing-parent states, and post-write listing refresh into `web/app/clients-view.tsx`; re-read the nested matter page after a client correct so flags are not inferred; do not GET-then-POST to predict a conflict
- [x] T017 [US2] Complete keyboard operation, visible focus, field/error associations, and non-color cues for the forms in `web/app/globals.css`, `web/app/client-form.tsx`, and `web/app/matter-form.tsx` against `specs/007-billing-operations-ui/mockups/05-clients.png` and `06-matters.png`
- [x] T018 [US2] Rebuild and sync `web/` into `src/LexTime.Api/wwwroot/`, then run Validation 3 and Validation 4 from `specs/009-party-directory-ui/quickstart.md` and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: Both stories are independently verifiable. SC-003 conflicts are visible as service text. SC-010 immutability holds because the fields are absent, not disabled.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Close documentation, mockup-scope, generated-export, and reviewer-reproducibility obligations without expanding the product surface.

- [x] T019 [P] Update `README.md` so Clients and Timekeepers are reached from the same two commands, Node is only needed to regenerate `web/`, and the UI remains a thin consumer rather than the repository's primary hiring signal
- [x] T020 [P] Review `web/` against `specs/009-party-directory-ui/contracts/party-directory-ui.md` and `specs/007-billing-operations-ui/mockups/05-clients.png`, `06-matters.png`, and `07-timekeepers.png`; remove search, count cards, billed/unbilled amounts, roles, practice areas, recent-entry widgets, Settings, Overview, a Matters destination, code/number inputs on correct, timekeeper writes, and a Time entries deep-link widget if any entered the implementation
- [x] T021 If `web/app/token-session.ts`, `src/LexTime.Api/Dashboard/DashboardFiles.cs`, or any SQL/auth file was touched, perform the P24 review and record it in `docs/agent-log.md`; if none were touched, record that fact in the same file instead of inventing a review
- [x] T022 Run `npm ci` and `npm run build` in `web/`, verify the committed `src/LexTime.Api/wwwroot/` exactly matches the fresh export, then run `dotnet build --warnaserror --no-incremental` followed by `dotnet test --no-build`
- [x] T023 Execute all five validations in `specs/009-party-directory-ui/quickstart.md` from a cold two-command start, including keyboard-only and tablet-width checks, and document genuine implementation friction in `docs/agent-log.md` or explicitly record that none occurred

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately
- **Foundational (Phase 2)**: Depends on Setup; blocks both stories
- **User Story 1 (Phase 3)**: Depends on Foundational; produces the MVP
- **User Story 2 (Phase 4)**: Depends on the User Story 1 Clients view (forms mount there); Timekeepers stays read-only
- **Polish (Phase 5)**: Depends on both desired stories; T019 and T020 can begin once behavior is stable; T021 precedes any implementation commit that touched auth; T022–T023 are final gates

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on User Story 2; independently demonstrable after Phase 2
- **User Story 2 (P2)**: Builds on User Story 1's Clients listing and nested matters, but has its own write/conflict walkthrough

### Within Each User Story

- Pin the host 401 before wiring the directories into the shell
- Shared API and state modules before the views
- Client table and Timekeepers view can be built in parallel, then Clients view assembled
- Write helpers and both forms can be built in parallel, then integrated
- Regenerate `wwwroot` after every completed UI story
- Stop at each checkpoint and validate before expanding scope

### Parallel Opportunities

- T002 and T003 touch independent TypeScript files
- T006 and T007 can proceed while T005 is written (different files)
- T013, T014, and T015 implement independent User Story 2 files
- T019 and T020 update independent documentation / review surfaces

---

## Parallel Example: User Story 1

```text
Task T006: Implement `web/app/clients-table.tsx`
Task T007: Implement `web/app/timekeepers-view.tsx`
Task T005: Extend `tests/LexTime.IntegrationTests/DashboardHostTests.cs`
```

After T002–T004 and T006, T008 assembles the listing in `web/app/clients-view.tsx`.

## Parallel Example: User Story 2

```text
Task T013: Add writes to `web/app/parties-api.ts`
Task T014: Implement `web/app/client-form.tsx`
Task T015: Implement `web/app/matter-form.tsx`
```

After all three complete, T016 integrates them into `web/app/clients-view.tsx`.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases
2. Pin the host contract
3. Implement client table, nested matters, Timekeepers roster, and detail
4. Regenerate the committed static export
5. Stop and run the User Story 1 independent test
6. Ship/demo here if the P3 evening cap is reached

### Incremental Delivery

1. Setup + Foundation → shell destinations and paged GET clients
2. User Story 1 → directory MVP
3. User Story 2 → register / open / correct / close and visible 409s
4. Polish → mockup audit, documentation, clean export, build/test/quickstart gates

### Scope Guard

- The mockups are a visual source, not permission to build the product
- Do not add search, count cards, billed amounts, roles, Settings, Overview, or a Matters sidebar item
- Do not add a firm-wide matters table, a code/number field on correct, delete, or any timekeeper write
- Do not pre-check uniqueness in the browser; render the 409 document
- Do not rewrite `web/app/party-lookups.ts` onto directory pagination
- No new API endpoint, schema change, CORS policy, UI test framework, or Node step in the reviewer quickstart
- P3 cut first: Time entries deep-link (not tasked). Cut second: selected client in the hash. Never cut paging, 409 display, immutability-by-absence, or Reports / Time entries
- If the evening still overruns after those cuts: split timekeepers into 010 before treating this file as done. Do not omit Timekeepers from this spec silently

---

## Notes

- `[P]` tasks operate on different files and are safe to execute concurrently
- Every C# member and test method added here requires meaningful XML documentation (P25)
- Imports stay at the top of TypeScript modules; switches over view-state unions are exhaustive
- Generated `wwwroot` is reviewer-serving output; `web/` is the editable source
- Do not restate uniqueness or immutability as client-side limits that hide the problem document
- Commit only when explicitly requested; P17 expects spec, plan/tasks, and implementation history to remain legible
