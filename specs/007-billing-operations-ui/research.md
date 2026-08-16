# Research — Billing Dashboard (007)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected.
The weekly rollup's meaning is not re-decided here; it lives in
[feature 003](../003-weekly-billable-rollup/research.md) and its contract.

The PRD amendment that unblocks this spec is `a829911` on `main`
("Permit a thin operations UI now that the API is complete.").

---

## R1. Same-origin static export, so the quickstart stays two commands

**Decision.** Next.js builds with `output: 'export'`. The export is committed under
`src/LexTime.Api/wwwroot` and served by the existing `dotnet run`. The reviewer
opens `http://localhost:5202/`. Swagger, health, and `/api/v1/...` stay where they
are. Node is not on the reviewer path.

**Rationale.** Constitution P18 and the amended PRD §6: the operations UI is
reached from the same two commands, and Node is not required to demonstrate the
backend. A separate `next start` (or CORS, or a third command) fails that gate.
Static export is what makes Next.js a set of files ASP.NET Core already knows how
to serve, rather than a second process.

`dotnet build` must **not** invoke `npm`. That would make Node a prerequisite of
the .NET solution and break a reviewer who has only Docker and the SDK. Regenerating
`wwwroot` is a developer step when the UI source changes, stated in
[quickstart.md](./quickstart.md), not a hidden bootstrap step.

**Alternatives rejected.**

- *`next dev` / `next start` as the documented run.* A third command, and a second
  origin, which needs CORS the API does not have.
- *MSBuild `Exec` of `npm run build`.* Quietly adds Node to every `dotnet build`.
- *Reverse-proxy from Next.js to the API, API not serving UI.* Reviewer still needs
  Node to see the dashboard.

**Trade-off (P19).** Generated files live in git, the same class of committed
evidence as `docs/performance.md`'s captures. The source of truth for edits is
`web/`; `wwwroot` is what the two-command path serves. Stale `wwwroot` after a
`web/` edit is a review item, not a runtime fallback.

---

## R2. Next.js 16 App Router, React 19, mockup chrome, no component kit

**Decision.** Next.js 16 (App Router, TypeScript) and React 19. `images.unoptimized`
is required by static export. The look follows
[mockups/03-weekly-billable-rollup.png](./mockups/03-weekly-billable-rollup.png)
and the shared navy / serif-wordmark chrome in [mockups/README.md](./mockups/README.md).
Ordinary CSS variables, not Tailwind, not a component library. One page at `/` —
the landing *is* the rollup (FR-001).

**Rationale.** TC-001 requires Next.js and React; versions are a planning choice.
Context7's current Next.js line is 16.x with App Router and TypeScript as
`create-next-app` defaults. Static export disables server actions, the default
image optimizer, and intercepting routes — none of which this slice needs. All
data fetching is a client `fetch` to the same origin.

The mockups are the visual contract for screens this repository will eventually
have. This slice takes their chrome and the rollup *table*. It does not take
charts, export, or working nav to screens that are 008, 009, or out of scope
(R10). Tailwind remains a `create-next-app` default we turn off: the mockup is
the design, a utility-class dialect is not.

**Alternatives rejected.**

- *Pages Router.* Would work for static export; App Router is the current default
  and does not cost us anything if we stay on client components.
- *SSR / `next start`.* Needs a Node server at review time (R1).
- *Tailwind or a component kit.* A second design system next to the mockups.
- *Shipping the Overview charts because they look finished.* The API cannot
  feed them (R10).

---

## R3. The existing minted token, pasted once

**Decision.** The dashboard has a sign-in field. The user pastes the bearer token
`Initialize-LocalDb.ps1` already prints. It is kept in `sessionStorage` and sent as
`Authorization: Bearer …` on the rollup `fetch`. There is no login endpoint, no
refresh, and no new identity provider. An expired or missing token is a sign-in
prompt, not a stack trace (FR-007). Selected dates and client filter stay in the
form across that prompt (FR-008).

**Rationale.** The spec assumes an existing session supplied by the environment.
The environment already mints a token in command one. Pasting it is using that
output, not a third command and not a new auth model (FR-014). `sessionStorage`
dies with the tab, which is closer to a session than `localStorage`.

The bootstrap's "paste into the Swagger authorize box" line is updated to mention
the dashboard field as well, so the token's two uses are documented in one place.

**Alternatives rejected.**

- *A new `/token` or `/login` endpoint.* A new identity surface. §2.2 still
  excludes a real IdP; this would look like starting one.
- *Embedding the signing key in the browser.* Turns the symmetric-dev-key shortcut
  into a client-side secret. P24 would reject it.
- *Query-string token as the only mechanism.* Ends up in logs and history. A paste
  field can still accept a one-time `?access_token=` for convenience, but it is not
  the stored form.

---

## R4. Initial range is the eight weeks ending on the seed reference date

**Decision.** On first load the inclusive range is **2026-06-18** .. **2026-08-13**,
shown as selected values the user can change. `2026-08-13` is feature 004's seed
reference date, already a load-bearing constant. Eight weeks is enough to see
multiple ISO weeks, standings, and a mix of numeric and null prior-week deltas,
without dumping the full 24-month seed.

**Rationale.** The API has no default range — a report that silently picks its own
is worse than one that refuses. The UI may pre-fill, provided the values are
visible and correctable (spec assumption). A labelled seed window makes SC-001's
two-minute walkthrough possible on a cold open. Using the clock ("this ISO week")
would move every time the reviewer opened it and would miss the seed.

**Alternatives rejected.**

- *Empty dates until the user applies.* Safer against silent defaults; fails SC-001
  for anyone who does not already know a working window.
- *Full seeded history.* A wall of rows. The rollup is still correct; it is no
  longer readable in two minutes.
- *`TimeProvider` "today".* Right for rule 4 in feature 005; wrong here, because
  the interesting data is anchored on 2026-08-13.

---

## R5. Client filter is a display restriction of the fetched period

**Decision.** One `GET` for the selected range, **without** `clientId`. The filter
control is filled from distinct `clientId` / `clientCode` / `clientName` on those
rows. Choosing a client hides the other rows. Standing on a remaining row is the
service's `clientRankInWeek` and is not recomputed.

**Rationale.** FR-004 requires a one-client view and service-provided standing.
The rollup already computes standing among all clients in the week, whether or not
`clientId` is passed. Filtering in the UI after one unfiltered fetch preserves that
number, fills the picker without calling `GET /api/v1/clients` (feature 009), and
avoids a second round trip. Eight weeks × ~60 clients is a small payload.

An empty match (filter set, no rows for that client) is the empty-success state,
not a 404.

**Alternatives rejected.**

- *Passing `clientId` to the API.* Also correct, and required if the payload ever
  grew. It needs a second source for the picker (the client list, or a previous
  unfiltered fetch). Extra machinery for no new meaning.
- *A free-text client id field.* Satisfies FR-004 with worse SC-001.
- *`GET /api/v1/clients` for the picker.* Pulls party management into this slice.

---

## R6. Inverted and incomplete ranges never leave the browser

**Decision.** If either date is missing, or `from` is later than `to`, the UI does
not `fetch`. It shows the same kind of actionable message the endpoint would, and
it does not present previous figures as current.

**Rationale.** Feature 003 already refuses those cases with 400. Sending them so
the UI can display the problem response works, but it is a network round trip to
learn something the form already knows, and it risks a slow failure looking like
"the service is down" (User Story 2 must keep those distinct). Mirror the rule,
don't wait for it.

**Alternatives rejected.**

- *Always send, always render the problem document.* Distinct from unavailable
  service only if the UI parses 400 separately — extra work for a case the form
  can refuse first.

---

## R7. Coverage stays on the host and the report, not a new UI runner

**Decision.** No Playwright, no Node in CI. xUnit + `WebApplicationFactory`
asserts: `GET /` is 200 and HTML without a bearer token; `GET /swagger` and
`GET /health` still work; the rollup route still returns 401 without a token.
The rollup's empty / zero / null-delta meaning remains feature 003's tests. The
dashboard walkthrough in [quickstart.md](./quickstart.md) is how SC-001 and SC-002
are shown, not a new browser harness.

**Rationale.** P13 weights domain rules and the reporting path. Those already have
real SQL Server tests. A Playwright stack would add Node to `azure-pipelines.yml`
and a second quality story the reviewer cannot run with `dotnet test`. P11 is
satisfied by the existing Testcontainers host; the new tests ride that host.

**Alternatives rejected.**

- *Playwright against Testcontainers.* Correct, and too much for this slice. A
  later spec may add it if 008's forms cannot be judged from the API tests.
- *No host test at all.* Then P18's "same `dotnet run` serves the dashboard" is
  an assertion. One 200 on `/` is the cheapest proof.

---

## R8. `MapDashboardFiles()` is composition, not a fourth Infrastructure site

**Decision.** An extension method on the API project registers default files and
static files from `wwwroot`. `Program.cs` calls it next to `MapReportEndpoints()`.
The files are anonymous. No endpoint names an `Infrastructure` type. No new
project reference.

**Rationale.** P21 wants `Program.cs` as a table of contents. P4's enumerated
Infrastructure sites stay at three. Serving `wwwroot` is ASP.NET Core static
files, not a use case and not a `DbContext`.

**Alternatives rejected.**

- *Inlining `UseStaticFiles` in `Program.cs`.* Works; it is the wall P21 exists
  to avoid once 008 adds fallback routes.
- *A fifth .NET project for the UI host.* Invents a layer P4 does not have.

---

## R9. No CORS, no new report endpoint, no new billing behaviour

**Decision.** Same origin makes CORS unnecessary. The dashboard calls
`GET /api/v1/reports/weekly-billable-rollup` as feature 003 shipped it. No query
parameter, no extra field, no client-side standing math.

**Rationale.** FR-012 and FR-013. The interesting bugs in this slice are display
bugs (null coalesced to zero, stale figures, standing-of-one). They are not
fixed by changing the report.

**Watch item.** `HoursDeltaVsPriorWeek: null` must render as "no comparison" (or
equivalent text), never as `0`. Coalescing in the view is the one-line defect
that undoes feature 003's FR-008.

---

## R10. The Codex mockups are the visual source; only the rollup screen is in scope

**Decision.** Treat
`C:\Users\mcgun\.codex\generated_images\01a00621-8478-7d61-803b-3f7d6e8edce0`
as the design set, with an in-repo copy under [mockups/](./mockups/). This spec
implements the **Reports → Weekly billable rollup** screen and the sign-in
*chrome*. The other six screens inventory later features or stay out of scope.
Inventory and exclusions: [mockups/README.md](./mockups/README.md).

Sign-in uses the split navy card from [01-sign-in.png](./mockups/01-sign-in.png)
with a **development-token** field. Email, password, remember-me, and forgot
password are not built (R3).

The header KPI cards on the rollup mockup may show **sums of the returned rows**
(billable hours, amount, distinct clients). They must not show a "vs prior
period" percentage — that number is not in the response, and inventing it fails
FR-013. Per-row `HoursDeltaVsPriorWeek` in the table is the comparison the
service actually computed.

**Rationale.** The mockups describe a product. The spec and the amended PRD
describe a thin consumer of one report. Following the mockup's table and chrome
makes SC-001 possible; following its charts and IA would re-inflate the spec
that P3 already split.

**Alternatives rejected.**

- *Build every mockup in this spec.* Repeats the P3 failure.
- *Ignore the mockups and ship an unstyled table.* The user supplied a visual
  contract; ordinary CSS can match it without a component kit (R2).
- *Leave mockups only on the Codex path.* A reviewer of this branch would not
  have them.
