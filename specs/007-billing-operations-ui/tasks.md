# Tasks: Billing Dashboard

**Input**: Design documents from `/specs/007-billing-operations-ui/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/dashboard-ui.md`, `quickstart.md`

**Tests**: The spec requires independent acceptance checks. Automated coverage stays at the ASP.NET Core host boundary; the approved plan deliberately adds no Playwright or Node test runner (R7, P13).

**Organization**: Tasks are grouped by user story so the headline rollup is a complete MVP before the nuanced empty, zero, authentication, and failure states are added.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on an incomplete task
- **[Story]**: Maps the task to User Story 1 or User Story 2
- Every task names the exact file or directory it changes or validates

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the source-controlled Next.js application and deterministic static-export path without putting Node on the reviewer quickstart.

- [x] T001 Initialize the Next.js 16 / React 19 TypeScript App Router package without Tailwind or a component library in `web/package.json`, `web/package-lock.json`, `web/tsconfig.json`, `web/next-env.d.ts`, and `web/next.config.ts`; configure `output: 'export'` and unoptimized images per R1–R2
- [x] T002 Add the deterministic export-sync script in `web/scripts/sync-export.mjs` and package scripts in `web/package.json` so `npm run build` replaces `src/LexTime.Api/wwwroot/` from `web/out/` without making `dotnet build` invoke npm

**Checkpoint**: `npm ci` and `npm run build` can produce and sync a static export, but the dashboard itself is not implemented.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the same-origin host, shared report contract, token session, and mockup-derived shell used by both stories.

**CRITICAL**: Complete this phase before either user story.

- [x] T003 [P] Add the fully XML-documented `MapDashboardFiles()` extension in `src/LexTime.Api/Dashboard/DashboardFiles.cs`, call it from `src/LexTime.Api/Program.cs`, and update the purpose text in `src/LexTime.Api/LexTime.Api.csproj` without changing the authorization of `/api/v1/*`
- [x] T004 [P] Define the exact feature-003 response types, same-origin rollup request, currency/hour/week formatters, and problem-safe error boundary in `web/app/reporting.ts`; do not calculate standings or prior-period percentages locally
- [x] T005 [P] Implement development-token read/write/clear and bearer-header creation with `sessionStorage` in `web/app/token-session.ts`; do not add email/password fields, a signing key, refresh logic, query-string persistence, or a new identity endpoint
- [x] T006 Create the shared App Router layout and mockup chrome in `web/app/layout.tsx` and `web/app/globals.css`: navy sidebar, serif LexTime wordmark, Reports as the only active destination, sans-serif content, accessible skip link, and no working navigation to 008/009 screens

**Checkpoint**: The API can serve static files anonymously, while report data remains protected; shared browser contracts and visual tokens are ready.

---

## Phase 3: User Story 1 — Understand the billing workload (Priority: P1) MVP

**Goal**: An authenticated operator can open the seeded weekly rollup, change its inclusive range, filter to one client, and read every service-provided figure without losing all-client standing.

**Independent Test**: With the seeded database and printed token, open `/`, apply `2026-06-18` through `2026-08-13`, compare a row with the existing rollup JSON, select one client, and confirm only that client's rows remain while each `clientRankInWeek` is unchanged.

### Tests for User Story 1

> Write the host test first and confirm it fails before the static export and host mapping exist.

- [x] T007 [US1] Add XML-documented integration tests in `tests/LexTime.IntegrationTests/DashboardHostTests.cs` proving unauthenticated `GET /` returns 200 HTML while the existing rollup route still returns 401 without a token; use the real Testcontainers host and review generated assertions line by line (P11, P15)

### Implementation for User Story 1

- [x] T008 [P] [US1] Implement visible inclusive from/to controls, the fixed initial range `2026-06-18`–`2026-08-13`, and a client selector populated only from returned rollup rows in `web/app/report-controls.tsx`
- [x] T009 [P] [US1] Implement the mockup-aligned rollup table in `web/app/rollup-table.tsx` with client, ISO week, billable hours, non-billable hours, amount, cumulative hours, delta, and rank columns; preserve numeric zero and the service-provided rank
- [x] T010 [US1] Implement the token-paste landing, authenticated same-origin fetch, period labelling, client-only display filtering, and optional service-derived summary cards in `web/app/page.tsx`; do not send `clientId`, recompute rank, add charts/export, or expose time-entry and party actions
- [x] T011 [US1] Build `web/`, sync the export into `src/LexTime.Api/wwwroot/`, and make `DashboardHostTests` pass while confirming `/swagger`, `/health`, and existing API routes retain their prior behavior
- [x] T012 [US1] Run Validation 1 and Validation 3 from `specs/007-billing-operations-ui/quickstart.md` against seeded SQL Server, compare at least one displayed row with the authenticated JSON response, and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: User Story 1 is independently usable as the dashboard MVP. Stop here if the one-evening cap is reached.

---

## Phase 4: User Story 2 — Tell empty, zero, and failure apart (Priority: P2)

**Goal**: The dashboard presents empty data, zero billable value, missing comparison, blocked input, expired access, loading, and unavailable service as distinct states with safe next actions.

**Independent Test**: Exercise the six User Story 2 outcomes from `quickstart.md`; each must be recognizably different, preserve the selected range/filter when recoverable, expose no internal diagnostics, and never present stale figures as current.

### Implementation for User Story 2

- [x] T013 [P] [US2] Define exhaustive discriminated view states and range validation in `web/app/dashboard-state.ts`, including loading, ready, empty, blocked-range, unauthenticated, and unavailable variants with a `never` default check
- [x] T014 [P] [US2] Implement accessible status, retry, validation, empty, and development-token prompt components with live-region/error associations in `web/app/dashboard-status.tsx`
- [x] T015 [US2] Integrate the exhaustive states into `web/app/page.tsx`: block incomplete/inverted ranges before fetch, clear stale rows when a new request starts, preserve controls across recoverable failures, clear the token on 401, and expose a safe retry without internal problem details
- [x] T016 [US2] Update `web/app/rollup-table.tsx` so existing zero values remain numeric, `hoursDeltaVsPriorWeek: null` renders explicit no-comparison text rather than `0`, and a client-filter miss renders empty success rather than 404/failure
- [x] T017 [US2] Complete keyboard, focus, non-color cues, desktop/tablet layout, and no-primary-horizontal-scroll styling in `web/app/globals.css` against `mockups/01-sign-in.png` and `mockups/03-weekly-billable-rollup.png`
- [x] T018 [US2] Rebuild and sync `web/` into `src/LexTime.Api/wwwroot/`, then run Validation 2 and Validation 4 from `specs/007-billing-operations-ui/quickstart.md` and record any discrepancy and its fix in `docs/agent-log.md`

**Checkpoint**: Both stories are independently verifiable, and all SC-002 state distinctions are visible.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Close documentation, security, generated-export, and reviewer-reproducibility obligations without expanding the product surface.

- [x] T019 [P] Update the token output text in `scripts/Initialize-LocalDb.ps1` to name both the Swagger authorize box and dashboard token field without changing token creation or printing the token anywhere new
- [x] T020 [P] Update `README.md` to state that the same two commands serve the dashboard, that Node is only needed to regenerate `web/`, and that the UI is a thin consumer rather than the repository's primary hiring signal
- [x] T021 Perform the P24 manual security review of browser token handling and the `scripts/Initialize-LocalDb.ps1` auth-adjacent change, verify no signing key/token enters committed `web/` or `src/LexTime.Api/wwwroot/`, and record the review and any accepted findings in `docs/agent-log.md`
- [x] T022 Review `web/` against `specs/007-billing-operations-ui/mockups/README.md` and remove any chart, export control, email/password flow, settings, fabricated comparison KPI, or working 008/009 navigation that entered the implementation
- [x] T023 Run `npm ci` and `npm run build` in `web/`, verify the committed `src/LexTime.Api/wwwroot/` exactly matches the fresh export, then run `dotnet build --warnaserror --no-incremental` followed by `dotnet test --no-build`
- [x] T024 Execute all five validations in `specs/007-billing-operations-ui/quickstart.md` from a cold two-command start, including keyboard-only and tablet-width checks, and document genuine implementation friction in `docs/agent-log.md` or explicitly record that none occurred

---

## Phase 6: Local Table Pagination

**Purpose**: Keep the 495-row seeded report readable without changing the rollup endpoint or any report meaning.

- [x] T025 Add accessible client-side rollup pagination in `web/app/page.tsx`, `web/app/rollup-table.tsx`, and `web/app/globals.css` with 20, 50, and 100 rows per page; paginate after client filtering and reset to page 1 when the range, client, or page size changes
- [x] T026 Update `README.md`, rebuild and sync the static export, verify `web/out/` matches `src/LexTime.Api/wwwroot/`, then repeat the TypeScript and .NET quality gates

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately
- **Foundational (Phase 2)**: Depends on Setup; blocks both stories
- **User Story 1 (Phase 3)**: Depends on Foundational; produces the MVP
- **User Story 2 (Phase 4)**: Depends on the User Story 1 page/table integration
- **Polish (Phase 5)**: Depends on both desired stories; T019 and T020 can begin once behavior is stable, T021 must precede any implementation commit touching auth, and T023–T024 are final gates

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on User Story 2; independently demonstrable after Phase 2
- **User Story 2 (P2)**: Builds on User Story 1's page and table, but has its own state-focused acceptance walkthrough

### Within Each User Story

- Write and observe the failing host contract before implementing the host/page
- Shared types and token session before page orchestration
- Controls and table can be built in parallel, then assembled in `page.tsx`
- State model and status components can be built in parallel, then integrated
- Regenerate `wwwroot` after every completed UI story
- Stop at each checkpoint and validate before expanding scope

### Parallel Opportunities

- T003, T004, and T005 touch independent C#/TypeScript files
- T008 and T009 implement independent User Story 1 components
- T013 and T014 implement independent User Story 2 state/UI files
- T019 and T020 update independent documentation surfaces

---

## Parallel Example: User Story 1

```text
Task T008: Implement `web/app/report-controls.tsx`
Task T009: Implement `web/app/rollup-table.tsx`
```

After both complete, T010 assembles them in `web/app/page.tsx`.

## Parallel Example: User Story 2

```text
Task T013: Implement `web/app/dashboard-state.ts`
Task T014: Implement `web/app/dashboard-status.tsx`
```

After both complete, T015 integrates every state into `web/app/page.tsx`.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases
2. Write the failing host contract
3. Implement controls, table, and authenticated report load
4. Regenerate the committed static export
5. Stop and run the User Story 1 independent test
6. Ship/demo here if the P3 evening cap is reached

### Incremental Delivery

1. Setup + Foundation → same-origin static host and shared browser contracts
2. User Story 1 → readable weekly rollup MVP
3. User Story 2 → safe, distinct edge/error states
4. Polish → auth review, documentation, clean export, build/test/quickstart gates

### Scope Guard

- The mockups are a visual source, not permission to build the product
- Do not add daily charts, export, email/password, settings, notifications, or live navigation to later features
- KPI cards are the first cut; the rollup table and state distinctions are not cuttable
- No new API endpoint, schema change, CORS policy, UI test framework, or Node step in the reviewer quickstart

---

## Notes

- `[P]` tasks operate on different files and are safe to execute concurrently
- Every C# member and test method added here requires meaningful XML documentation (P25)
- Imports stay at the top of TypeScript modules; switches over view-state unions are exhaustive
- Generated `wwwroot` is reviewer-serving output; `web/` is the editable source
- Do not commit implementation before T021's security review if auth-adjacent files are included
- Commit only when explicitly requested; P17 expects spec, plan/tasks, and implementation history to remain legible
