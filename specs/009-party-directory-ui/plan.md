# Implementation Plan: Party Directory UI

**Branch**: `009-party-directory-ui` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-party-directory-ui/spec.md`

## Summary

Two more views in the existing Next.js static export: Clients and Timekeepers.
The reviewer pastes the same development token, pages the seeded directories,
registers a client, opens a matter under it, and sees a uniqueness conflict as
the service wrote it — field and value, not a second copy of the uniqueness
rules in the browser. Timekeepers stay a labelled read-only roster.

Matters are not a third sidebar destination. They are listed on the selected
client (R2). That is the P3 save that keeps timekeepers in this spec rather
than splitting them into 010.

Reports and Time entries stay reachable in the same shell. Feature 006 remains
the write path. This feature is how a person operates it without Postman.

Two decisions carry it. **Server-side paging** (R3) is what keeps a directory
from becoming a payload. **Rendering the 409 conflict document and not
pre-checking uniqueness** (R5) is what keeps feature 006's meaning intact once
a form is in the way.

The PRD permission that covers this slice is already on `main` (`a829911`).

## Technical Context

**Language/Version**: C# 13 on .NET 9 (SDK 9.0.317) for the host; TypeScript on
Next.js 16 (App Router) and React 19 for `web/` — inherited from 007, not
re-chosen

**Primary Dependencies**: none added to the .NET solution. `web/` keeps Next.js
16 and React 19. Visual tokens from
[007 mockups 05–07](../007-billing-operations-ui/mockups/README.md);
no Tailwind, no component library, no mediator, no mapper

**Storage**: none in the UI. SQL Server 2022 remains behind the existing party
endpoints. No migration

**Testing**: xUnit against the existing Testcontainers host (P11, R8). No
Playwright. Uniqueness, immutability, and timekeeper-unwritable tests stay in
feature 006

**Target Platform**: desktop and tablet browsers, served from `LexTime.Api` at
`http://localhost:5202/`

**Project Type**: the same static Next.js consumer as 007, not a fifth .NET
project

**Performance Goals**: none stated, none measured. This feature makes no speed
claim

**Constraints**: two-command quickstart unchanged (FR-026, P18); no timekeeper
create/edit (FR-016, SC-010); no delete/merge/renumber (FR-017); no firm-wide
matters table (FR-007, R2); client listing filter is status only (FR-003);
page size 20 / 50 / 100 sent as `take` with `skip` (FR-004, R3); codes and
numbers absent from correction bodies (FR-014, R6); uniqueness refusals come
from the 409 document (R5); no new identity provider (FR-025)

**Scale/Scope**: two additional in-shell views, existing 006 routes, ~8 files
under `web/`, regenerated `wwwroot`. At the P3 cap because the forms are the
volume, not new backend work. Cut order is fixed in the Constitution Check.
Timekeepers stay in because matters are nested, not because three equal
screens were squeezed

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design —
result at the bottom of this section.*

Checked against `.specify/memory/constitution.md` v2.0.1 and `docs/prd.md` as
amended in `a829911`.

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ⚠️ **honestly, supporting** | The ten-minute read is still the rollup SQL, the six rules, and the layering. This puts the finished party path in a browser. Named as supporting, not as the new centre of gravity |
| P2 | PRD out-of-scope binding | ✅ | `a829911` permits a thin consumer of the finished API. This slice stays inside it: existing party *routes*, no product chrome, no new IdP, no invented counts or billed amounts, no timekeeper writes |
| P3 | One evening per spec | ⚠️ **at the cap** | 007/008 already paid for the scaffold. Nesting matters under Clients (R2) is the cut that keeps timekeepers here. Cut order fixed below. Not split now, because a 010 that is only a read-only table would duplicate shell, paging, and host-test cost for one screen |
| P4 | Four projects, dependencies inward | ✅ | `web/` has no `ProjectReference`. No new `MapDashboardFiles` Infrastructure site. No endpoint names an Infrastructure type |
| P5 | Right tool per access path | ✅ | UI calls the existing party endpoints. EF Core and `SqlCommand` stay where they are |
| P6 | Domain rules in domain and database | ✅ | Uniqueness and immutability stay in 006 / the schema. The UI displays 409 documents. It does not pre-check codes or invent a cascade |
| P7 | Procedures source-controlled | — | No procedure change |
| P8 | Performance claims measured | ✅ | No claim made. Feature 004's figures are not re-quoted |
| P9 | Seed realistic in shape | ✅ | Consumed as the walkthrough fixture. Client listing opens on all statuses so inactive seed rows are visible (R4) |
| P10 | The rollup is the headline | ✅ | Reports remains a first-class destination. Directories do not replace it |
| P11 | Real SQL Server | ✅ | Host tests ride the existing Testcontainers fixture. No in-memory fake of the listing |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged. Feature 006's uniqueness expectations are not re-derived from UI rendering |
| P13 | Deliberate coverage | ✅ | Host still proves `/` is HTML without a token and the client and timekeeper collections still require one. Collision pairs stay in 006. No Playwright (R8) |
| P14 | Spec before code | ✅ | Spec on this branch; plan follows; implementation after |
| P15 | Generated SQL reviewed | — | No SQL in this feature |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec, plan and implementation stay separate |
| P18 | Quickstart is two commands | ✅ | Same committed `wwwroot`, same `dotnet run`. `npm` is not a reviewer step (R1) |
| P19 | Trade-offs stated | ✅ | No Matters sidebar item, hash views not real URLs, no Playwright, P1 as supporting, 008 pickers still `take=200` — all named |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | No new registration. `app.MapDashboardFiles()` already exists |
| P22 | Branch per spec | ✅ | On `009-party-directory-ui` |
| P23 | Reproducible quality gate | ✅ | Pipeline stays `dotnet build`. `tsc` in `web/` is a developer check, not a merge gate |
| P24 | Security review before commit touching auth or SQL | ✅ | This slice reuses 007's token paste and `sessionStorage`. No new validation, no key in the browser, no SQL. If `DashboardFiles` or token handling is touched, a P24 pass is logged; the expected commit does not touch them |
| P25 | XML docs on everything | ✅ | New C# is host-test methods only; those carry XML docs. P25 does not bind TypeScript |

**Gate result: PASS.** Complexity Tracking stays empty. Two judgements (P1, P3)
are expanded here rather than waved through.

**P1, honestly.** A senior .NET reviewer who opens `web/` first will be less
confident, not more. The README and this plan say the UI is a consumer of the
finished API, and that it is the first thing cut if it starts looking like a
product. Party forms are how uniqueness and immutability are *demonstrated*,
not where they live.

**P3 cut order**, fixed now rather than improvised later:

1. **Cut first**: the optional Time entries deep-link (FR-027 MAY). The
   operator can still choose Time entries and set the matter or timekeeper
   filter by hand.
2. **Cut second**: putting the selected client in the hash. In-memory
   selection still satisfies FR-001 and FR-020 within a session.
3. **Never cut**: server-side paging against `total`; displaying every 409
   `conflictingField` / `conflictingValue`; no code or number on correct; no
   timekeeper write; no firm-wide matters table; no delete; Reports and Time
   entries still reachable; `/` served without a token while `/api/v1/clients`
   and `/api/v1/users` are not.
4. **Already cut, do not put back**: a Matters sidebar destination, search,
   summary count cards, billed/unbilled amounts, roles, practice areas, recent
   entries, Settings, Overview, Playwright, a new `GET /api/v1/matters`.
5. **If the evening still overruns after (1) and (2)**: stop and split
   timekeepers into 010 before `/speckit-tasks` is treated as done. Do not
   ship Clients without Timekeepers from this spec; that would be an unplanned
   scope change, not a cut.

**Post-Phase 1 re-check**: unchanged. No new .NET project, no new NuGet package,
no CORS, no new `/api/v1` route, no schema. `web/` remains the sibling §5
permits. The UI contract consumes feature 006; it does not extend it. 008's
picker module stays on `take=200` and is not rewritten into these screens.

## Project Structure

### Documentation (this feature)

```text
specs/009-party-directory-ui/
├── plan.md                      # This file
├── spec.md
├── research.md                  # Phase 0 — R1..R10
├── data-model.md                # Phase 1
├── quickstart.md                # Phase 1
├── checklists/requirements.md
├── contracts/
│   └── party-directory-ui.md    # Phase 1 — UI contract; API is 006's
└── tasks.md                     # /speckit-tasks output — not created here
```

Visual source remains
[`specs/007-billing-operations-ui/mockups/05-clients.png`](../007-billing-operations-ui/mockups/05-clients.png),
[`06-matters.png`](../007-billing-operations-ui/mockups/06-matters.png), and
[`07-timekeepers.png`](../007-billing-operations-ui/mockups/07-timekeepers.png).
This feature does not copy the mockup set.

### Source Code (repository root)

```text
web/
├── app/page.tsx                 # MOD — shell nav: Reports | Time entries | Clients | Timekeepers
├── app/globals.css              # MOD — directory / form / detail layout
├── app/token-session.ts         # unchanged — same sessionStorage key
├── app/party-lookups.ts         # unchanged — 008 pickers still take=200 (R10)
├── app/parties-api.ts           # NEW — paged list / get / register / correct; 409 conflicts
├── app/clients-view.tsx         # NEW — status filter, table, detail, nested matters, forms
├── app/clients-table.tsx        # NEW — paged client rows
├── app/client-form.tsx          # NEW — register (code+name) / correct (name+active)
├── app/matter-form.tsx          # NEW — open (number+name+default billable) / correct (name+flags)
├── app/timekeepers-view.tsx     # NEW — paged roster and read-only detail
└── package.json                 # MOD — description names the directories

src/LexTime.Api/wwwroot/         # MOD — regenerated static export

tests/LexTime.IntegrationTests/
└── DashboardHostTests.cs        # MOD — client and timekeeper collections still 401 without a token
```

No new file under `src/LexTime.Api/` is expected. `MapDashboardFiles()` already
serves the export at `/`.

**Structure Decision**: stay in the 007 `web/` tree and the same committed
`wwwroot`. Clients and Timekeepers are views of the existing single-page export
(R2), not extra Next.js routes that would need an ASP.NET fallback. 008's
`party-lookups.ts` is left for pickers so a paged directory does not silently
change how Time entries loads names.

## Complexity Tracking

> No Constitution Check violations. P1 and P3 are judgements inside a passing
> gate, expanded in the Constitution Check and in `research.md`.
