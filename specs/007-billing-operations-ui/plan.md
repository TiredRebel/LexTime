# Implementation Plan: Billing Dashboard

**Branch**: `007-billing-operations-ui` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-billing-operations-ui/spec.md`

## Summary

A Next.js static export of one page — the weekly rollup — served by the existing
`dotnet run`. The look follows the Codex mockups (navy sidebar, serif wordmark,
rollup table). The reviewer pastes the token command one already prints, sees the
seed window, and can tell empty from zero from "no prior-week comparison".

Charts, export, email/password, and working nav to time entries or parties are
in the mockup set and **not** in this slice (R10).

No new endpoint, no schema change, no Node on the reviewer path. The report is
still feature 003's. This feature is how a person looks at it without Postman.

Two decisions carry it. **Same-origin static files** (R1) are what keep P18 at
two commands. **Not coalescing a null delta to zero** (R9) is what keeps feature
003's meaning intact once a browser is in the way.

The PRD amendment that permits this is `a829911` (FR-016, SC-008).

## Technical Context

**Language/Version**: C# 13 on .NET 9 (SDK 9.0.317) for the host; TypeScript on
Next.js 16 (App Router) and React 19 for `web/`

**Primary Dependencies**: none added to the .NET solution. `web/` uses Next.js 16
and React 19. Visual tokens come from [mockups/](./mockups/); no Tailwind, no
component library, no mediator, no mapper (R2, R10)

**Storage**: none in the UI. SQL Server 2022 remains behind the existing rollup.
No migration

**Testing**: xUnit against the existing Testcontainers host (P11, R7). No
Playwright. The rollup's empty / zero / null-delta tests stay in feature 003

**Target Platform**: desktop and tablet browsers, served from `LexTime.Api` at
`http://localhost:5202/`

**Project Type**: a static Next.js consumer of the existing web service, not a
fifth .NET project

**Performance Goals**: none stated, none measured. This feature makes no speed
claim

**Constraints**: two-command quickstart unchanged (FR-015, P18); no time-entry or
party actions (FR-001, SC-007); standing among all clients; null prior-week delta
is not zero (FR-004, FR-006); no new identity provider (FR-014); pagination is
browser-side after filtering and never changes the single rollup request (FR-017)

**Scale/Scope**: one page, one `GET`, ~10 files in `web/`, one API extension
method, one host test. At the P3 cap because the scaffold is the volume, not the
logic

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — result
at the bottom of this section.*

Checked against `.specify/memory/constitution.md` v2.0.1 and `docs/prd.md` as
amended in `a829911`.

| # | Principle | Verdict | How this design satisfies it |
| --- | --- | --- | --- |
| P1 | Hiring signal | ⚠️ **honestly, supporting** | The ten-minute read is still the rollup SQL and the layering. This puts that headline in a browser. Named as supporting, not as the new centre of gravity |
| P2 | PRD out-of-scope binding | ✅ | `a829911` replaced "Any frontend / UI" with a bounded permission. This slice stays inside it: existing rollup only, no product chrome, no new IdP |
| P3 | One evening per spec | ⚠️ **at the cap** | Scaffold + one page + host wiring. Cut order fixed below |
| P4 | Four projects, dependencies inward | ✅ | `web/` has no `ProjectReference`. `MapDashboardFiles()` is ASP.NET Core static files, not a fourth Infrastructure site (R8) |
| P5 | Right tool per access path | ✅ | UI calls the existing rollup. EF Core and `SqlCommand` stay where they are |
| P6 | Domain rules in domain and database | ✅ | FR-012. The UI displays refusals and figures; it does not re-implement the report |
| P7 | Procedures source-controlled | — | No procedure change |
| P8 | Performance claims measured | ✅ | No claim made. Feature 004's figures are not re-quoted |
| P9 | Seed realistic in shape | ✅ | Consumed as the walkthrough fixture. Initial range is anchored on the seed reference date (R4) |
| P10 | The rollup is the headline | ✅ | This slice *is* the headline. Parties and time entry are 008/009 |
| P11 | Real SQL Server | ✅ | Host tests ride the existing Testcontainers fixture. No in-memory UI fake of the report |
| P12 | Hand-computed fixture | — | Feature 003's, unchanged. The UI must not derive expected totals from its own rendering |
| P13 | Deliberate coverage | ✅ | One host test that `/` is served and the rollup still requires a token. Empty vs zero vs null delta stay in 003. No Playwright (R7) |
| P14 | Spec before code | ✅ | Spec on this branch; plan follows; implementation after |
| P15 | Generated SQL reviewed | — | No SQL in this feature |
| P16 | Agent mistakes logged | ✅ | `docs/agent-log.md` continues |
| P17 | Separate commits | ✅ | Spec, plan and implementation stay separate. The PRD amendment already landed on `main` as its own commit |
| P18 | Quickstart is two commands | ✅ | Static export in `wwwroot`, served by `dotnet run`. `npm` is not a reviewer step (R1) |
| P19 | Trade-offs stated | ✅ | Committed `wwwroot`, no Tailwind, no Playwright, P1 as supporting — all named |
| P20 | English | ✅ | |
| P21 | Composition roots are extension methods | ✅ | `app.MapDashboardFiles()` |
| P22 | Branch per spec | ✅ | On `007-billing-operations-ui` |
| P23 | Reproducible quality gate | ✅ | No new UI quality claim in the README. The pipeline stays `dotnet build`. `tsc` in `web/` is a developer check, not a merge gate |
| P24 | Security review before commit touching auth or SQL | ✅ | Review task. Token paste and `sessionStorage` are auth-adjacent; no new validation, no key in the browser (R3) |
| P25 | XML docs on everything | ✅ | On the new C# extension. P25 does not bind TypeScript |

**Gate result: PASS.** Complexity Tracking stays empty. Two judgements (P1, P3)
are expanded here rather than waved through.

**P1, honestly.** A senior .NET reviewer who opens `web/` first will be less
confident, not more. The README and this plan say the UI is a consumer of the
rollup, and that it is the first thing cut if it starts looking like a product.
That is the amendment's bargain, not a claim that React is now the signal.

**P3 cut order**, fixed now rather than improvised later:

1. **Cut first**: KPI summary cards. The table already carries the figures.
2. **Cut second**: the client-filter `<select>`; a visible client-id field still
   satisfies FR-004.
3. **Never cut**: same-origin hosting, mockup table columns, empty vs zero vs
   null delta, standing among all clients, `/` served without a token while the
   rollup is not.
4. **Already cut, do not put back**: daily charts, export, vs-prior KPI
   percentages, live nav to 008/009 screens (R10).

**Post-Phase 1 re-check**: unchanged. No new .NET project, no new NuGet package,
no CORS, no report field. `web/` is the sibling §5 now permits.

## Project Structure

### Documentation (this feature)

```text
specs/007-billing-operations-ui/
├── plan.md                      # This file
├── spec.md
├── research.md                  # Phase 0 — R1..R9
├── data-model.md                # Phase 1
├── quickstart.md                # Phase 1
├── checklists/requirements.md
├── contracts/
│   └── dashboard-ui.md          # Phase 1 — UI + host contract; API is 003's
├── mockups/                     # Codex screens; README maps in/out of this slice
└── tasks.md                     # /speckit-tasks output — not created here
```

### Source Code (repository root)

```text
web/                                 # NEW — Next.js 16 App Router, static export
├── app/page.tsx                     # NEW — the dashboard (client fetch)
├── app/layout.tsx                   # NEW
├── app/globals.css                  # NEW — ordinary CSS, no Tailwind
├── next.config.ts                   # NEW — output: 'export', images.unoptimized
└── package.json                     # NEW

src/LexTime.Api/
├── Dashboard/DashboardFiles.cs      # NEW — MapDashboardFiles() (P21, R8)
├── wwwroot/                         # NEW — committed export served at /
├── Program.cs                       # MOD — app.MapDashboardFiles()
└── LexTime.Api.csproj               # MOD — <Description> names the static files

scripts/Initialize-LocalDb.ps1       # MOD — token line also names the dashboard field

tests/LexTime.IntegrationTests/
└── DashboardHostTests.cs            # NEW — GET / is 200 HTML; rollup still 401
```

**Structure Decision**: `web/` is a sibling of `src/`, as `docs/prd.md` §5 now
allows, not a fifth layered project. The committed export lives in the API's
`wwwroot` because that is what `dotnet run` can serve without Node (R1). Putting
the Next.js app inside `LexTime.Api` would mix a Node tree into a .NET project
and make the "four projects" read as five folders of a different kind.

## Complexity Tracking

> No Constitution Check violations. P1 and P3 are judgements inside a passing
> gate, expanded in the Constitution Check and in `research.md`.
