# Implementation Plan: Time Entry Operations UI

**Branch**: `008-time-entry-ui` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-time-entry-ui/spec.md`

## Summary

A second view in the existing Next.js static export: Time entries. The reviewer
pastes the same development token, lists a bounded page of the 400k seed,
records six minutes, and sees a domain-rule refusal as the service wrote it —
not as a second copy of the six rules in the browser.

The weekly rollup stays reachable in the same shell. Clients, matters, and
timekeepers are consumed read-only for names and pickers; creating them is 009.

No new endpoint, no schema change, no Node on the reviewer path. Feature 005
remains the write path. This feature is how a person operates it without
Postman.

Two decisions carry it. **Server-side paging** (R3) is what keeps the seed from
becoming a payload. **Rendering `violations[]` and not re-implementing the
rules** (R8) is what keeps feature 005's meaning intact once a form is in the
way.

The PRD permission that covers this slice is already on `main` (`a829911`).

## Technical Context

**Language/Version**: C# 13 on .NET 9 (SDK 9.0.317) for the host; TypeScript on
Next.js 16 (App Router) and React 19 for `web/` — inherited from 007, not
re-chosen

**Primary Dependencies**: none added to the .NET solution. `web/` keeps Next.js
16 and React 19. Visual tokens from
[007 mockup 04](../007-billing-operations-ui/mockups/04-time-entries.png);
no Tailwind, no component library, no mediator, no mapper

**Storage**: none in the UI. SQL Server 2022 remains behind the existing
time-entry and party endpoints. No migration

**Testing**: xUnit against the existing Testcontainers host (P11, R9). No
Playwright. Domain-rule accepting and refusing tests stay in feature 005

**Target Platform**: desktop and tablet browsers, served from `LexTime.Api` at
`http://localhost:5202/`

**Project Type**: the same static Next.js consumer as 007, not a fifth .NET
project

**Performance Goals**: none stated, none measured. This feature makes no speed
claim

**Constraints**: two-command quickstart unchanged (FR-020, P18); no party
create/edit (FR-001, FR-021, SC-009); listing filters are work-date / timekeeper
/ matter only (FR-003); page size 20 / 50 / 100 sent as `take` with `skip`,
never the whole table (FR-004, R3); rate is never an input (FR-007); timekeeper
is not revisable (FR-008); domain refusals come from the problem document (R8);
no new identity provider (FR-019)

**Scale/Scope**: one additional in-shell view, five existing time-entry routes,
read-only party lookups, ~8 files under `web/`, regenerated `wwwroot`. At the
P3 cap because the forms are the volume, not new backend work. Cut order is
fixed in the Constitution Check

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design —
result at the bottom of this section.*

Checked against `.specify/memory/constitution.md` v2.0.1 and `docs/prd.md` as
amended in `a829911`.

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ⚠️ **honestly, supporting** | The ten-minute read is still the rollup SQL, the six rules, and the layering. This puts the finished write path in a browser. Named as supporting, not as the new centre of gravity |
| P2 | PRD out-of-scope binding | ✅ | `a829911` permits a thin consumer of the finished API. This slice stays inside it: existing time-entry and party *reads*, no product chrome, no new IdP, no invented totals |
| P3 | One evening per spec | ⚠️ **at the cap** | 007 already paid for the scaffold. This spec is one view + forms + problem rendering. Cut order fixed below. Not split, because the two user stories share the listing and the shell; splitting would duplicate that cost |
| P4 | Four projects, dependencies inward | ✅ | `web/` has no `ProjectReference`. No new `MapDashboardFiles` Infrastructure site. No endpoint names an Infrastructure type |
| P5 | Right tool per access path | ✅ | UI calls the existing time-entry and party endpoints. EF Core and `SqlCommand` stay where they are |
| P6 | Domain rules in domain and database | ✅ | FR-010 / R8. The UI displays `violations[]`. It does not re-implement increment, daily maximum, or the backdating window |
| P7 | Procedures source-controlled | — | No procedure change |
| P8 | Performance claims measured | ✅ | No claim made. Feature 004's figures are not re-quoted |
| P9 | Seed realistic in shape | ✅ | Consumed as the walkthrough fixture. Listing opens on the last seed week (R4) |
| P10 | The rollup is the headline | ✅ | Reports remains a first-class destination. Time entries does not replace it |
| P11 | Real SQL Server | ✅ | Host tests ride the existing Testcontainers fixture. No in-memory fake of the listing |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged. Feature 005's rule expectations are not re-derived from UI rendering |
| P13 | Deliberate coverage | ✅ | Host still proves `/` is HTML without a token and the time-entry routes still require one. Rule pairs stay in 005. No Playwright (R9) |
| P14 | Spec before code | ✅ | Spec on this branch; plan follows; implementation after |
| P15 | Generated SQL reviewed | — | No SQL in this feature |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec, plan and implementation stay separate |
| P18 | Quickstart is two commands | ✅ | Same committed `wwwroot`, same `dotnet run`. `npm` is not a reviewer step (R1) |
| P19 | Trade-offs stated | ✅ | Hash views not real URLs, matter names resolved per page, no Playwright, P1 as supporting — all named |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | No new registration. `app.MapDashboardFiles()` already exists |
| P22 | Branch per spec | ✅ | On `008-time-entry-ui` |
| P23 | Reproducible quality gate | ✅ | Pipeline stays `dotnet build`. `tsc` in `web/` is a developer check, not a merge gate |
| P24 | Security review before commit touching auth or SQL | ✅ | This slice reuses 007's token paste and `sessionStorage`. No new validation, no key in the browser, no SQL. If `DashboardFiles` or token handling is touched, a P24 pass is logged; the expected commit does not touch them |
| P25 | XML docs on everything | ✅ | No new public C# surface expected. P25 does not bind TypeScript |

**Gate result: PASS.** Complexity Tracking stays empty. Two judgements (P1, P3)
are expanded here rather than waved through.

**P1, honestly.** A senior .NET reviewer who opens `web/` first will be less
confident, not more. The README and this plan say the UI is a consumer of the
finished API, and that it is the first thing cut if it starts looking like a
product. Time-entry forms are how the six rules are *demonstrated*, not where
they live.

**P3 cut order**, fixed now rather than improvised later:

1. **Cut first**: resolving matter display names on every list row. Matter
   `#id` in the table, name in the detail pane and in the picker, still
   satisfies FR-002's MAY.
2. **Cut second**: hash destinations. An in-memory view switch still satisfies
   FR-001.
3. **Never cut**: server-side paging against `total`; displaying every returned
   `violations[]` entry; no rate field; no timekeeper change on revise; delete
   confirmation; Reports still reachable; `/` served without a token while
   `/api/v1/time-entries` is not.
4. **Already cut, do not put back**: search, trend cards, realization,
   draft/posted status, Settings, party management screens, a client-id listing
   filter, Playwright, a new join endpoint that returns names on the listing.

**Post-Phase 1 re-check**: unchanged. No new .NET project, no new NuGet package,
no CORS, no new `/api/v1` route, no schema. `web/` remains the sibling §5
permits. The UI contract consumes feature 005 and 006; it does not extend them.

## Project Structure

### Documentation (this feature)

```text
specs/008-time-entry-ui/
├── plan.md                      # This file
├── spec.md
├── research.md                  # Phase 0 — R1..R10
├── data-model.md                # Phase 1
├── quickstart.md                # Phase 1
├── checklists/requirements.md
├── contracts/
│   └── time-entry-ui.md         # Phase 1 — UI contract; API is 005's and 006's
└── tasks.md                     # /speckit-tasks output — not created here
```

Visual source remains
[`specs/007-billing-operations-ui/mockups/04-time-entries.png`](../007-billing-operations-ui/mockups/04-time-entries.png).
This feature does not copy the mockup set.

### Source Code (repository root)

```text
web/
├── app/page.tsx                 # MOD — shell nav: Reports | Time entries
├── app/globals.css              # MOD — listing / form / detail layout
├── app/token-session.ts         # unchanged — same sessionStorage key
├── app/time-entries-api.ts      # NEW — list / get / record / revise / delete
├── app/party-lookups.ts         # NEW — read-only users / clients / matters
├── app/time-entries-view.tsx    # NEW — filters, table, detail, form
├── app/time-entries-table.tsx   # NEW — paged rows
├── app/time-entry-form.tsx      # NEW — record / revise / delete confirm
└── package.json                 # MOD — description names both views

src/LexTime.Api/wwwroot/         # MOD — regenerated static export

tests/LexTime.IntegrationTests/
└── DashboardHostTests.cs        # MOD — time-entry collection still 401 without a token
```

No new file under `src/LexTime.Api/` is expected. `MapDashboardFiles()` already
serves the export at `/`.

**Structure Decision**: stay in the 007 `web/` tree and the same committed
`wwwroot`. Time entries is a view of the existing single-page export (R2), not
a second Next.js route that would need an ASP.NET fallback. Party lookups live
in their own module so 009 can replace them with directory screens later
without rewriting the listing.

## Complexity Tracking

> No Constitution Check violations. P1 and P3 are judgements inside a passing
> gate, expanded in the Constitution Check and in `research.md`.
