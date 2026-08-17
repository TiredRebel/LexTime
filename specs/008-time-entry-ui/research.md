# Research — Time Entry Operations UI (008)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected.
The six domain rules are not re-decided here; they live in
[feature 005's contract](../005-time-entries-and-rules/contracts/domain-rules.md).
The shell, token session, and static export are not re-decided here; they live
in [feature 007's research](../007-billing-operations-ui/research.md) (R1–R3, R8).

The PRD permission that unblocks this spec is `a829911` on `main`
("Permit a thin operations UI now that the API is complete.").

---

## R1. Same origin, same two commands, same token field

**Decision.** Keep 007's static export under `src/LexTime.Api/wwwroot`, served by
the existing `dotnet run`. Keep the development-token paste and
`sessionStorage` key `lextime.development-token`. Node is still not on the
reviewer path. Regenerating `wwwroot` remains a developer step when `web/`
changes.

**Rationale.** Constitution P18 and FR-020. A second origin, a login endpoint,
or an `npm` step on `dotnet build` would fail the same gates 007 already
passed. Time entries is another consumer of the same session, not a new auth
model (FR-019).

**Alternatives rejected.**

- *`next start` plus CORS.* Third command, second origin, new API behaviour.
- *A new `/login` or `/token` route.* Looks like starting an IdP (§2.2).
- *A second static site at `/entries`.* Extra host wiring for no new meaning
  once R2 is taken.

---

## R2. In-shell views at `/`, hash destinations

**Decision.** One exported page at `/`. The navy sidebar gains **Time entries**
and keeps **Reports**. The selected destination is `location.hash`:
`#time-entries` or `#reports` (default). Refresh keeps the view. No
`MapFallbackToFile`, no extra `wwwroot` folder.

**Rationale.** Static export of a second App Router route (`/time-entries`)
needs ASP.NET to serve `time-entries/index.html` for `/time-entries` without a
trailing slash. 007's R8 already named that as the wall P21 exists to avoid.
A hash is bookmarkable and needs no host change. FR-001 requires a destination,
not a distinct URL.

**Alternatives rejected.**

- *A real `/time-entries` route plus fallback middleware.* Correct, and a new
  composition site for a demo that already loads one HTML file.
- *Query `?view=`.* Survives less cleanly next to the rollup's own dates, and
  still reloads the whole export for no gain.

**Trade-off (P19).** The address bar does not look like a multi-page app. The
sidebar does. That is enough for SC-001 and cheaper than a fallback.

---

## R3. The listing is paged by the service, never by the browser

**Decision.** `GET /api/v1/time-entries` with `skip` and `take`. Page sizes are
**20, 50, and 100** — the same choices as the rollup table, all under the API's
maximum of 200. `skip = (page - 1) * take`. The footer shows the matching
`total`, not the page length. Changing range, timekeeper, matter, or page size
resets to page 1.

**Rationale.** FR-004. The seed is 400,000 rows. 007 could page in the browser
because one rollup response is small. Doing that here would either freeze the
tab or invent a new "give me everything" call the API correctly refuses.

**Alternatives rejected.**

- *Default `take` only, no page-size control.* Satisfies FR-004 with a worse
  SC-001.
- *`take=200` always.* Legal, louder than the rollup's 20/50/100, and still not
  the whole table.
- *A new list endpoint that joins names and returns a smaller page.* New API
  behaviour this spec is not allowed to add.

---

## R4. Listing opens on the last seed week; recording defaults to today

**Decision.** On first load of Time entries the inclusive work-date filter is
**2026-08-10** .. **2026-08-13**, shown as selected values the user can change.
`2026-08-13` is the seed reference date; the 10th is the Monday of that ISO
week. The record form's work date defaults to **the browser's current local
date**, also visible and correctable.

**Rationale.** The listing must show seed rows in two minutes (SC-001). Opening
on 007's eight-week rollup window would still match tens of thousands of
entries — paged, but slow to scan. Opening on "today" would miss the seed
entirely once the clock leaves August 2026.

The record default cannot be the seed date. After the 90-day backdating window
moves past 2026-08-13, a walkthrough that records against the seed date is
refused by rule 4 for a reason that has nothing to do with the form. "Today"
is what rule 4 is for.

**Alternatives rejected.**

- *Empty dates until Apply.* Safer against silent defaults; fails SC-001.
- *Clock-based listing range.* Right for recording; wrong for finding seed
  rows.
- *Seed date as the record default.* Walkthrough-rot, the same class of defect
  CLAUDE.md already records for tests that hard-code "today".

---

## R5. Matter choice is client-then-matter; client is not a listing filter

**Decision.** There is no flat matter collection. The existing list is
`GET /api/v1/clients/{clientId}/matters`. The UI therefore offers an optional
**client** control whose only job is to load that client's matters for the
matter picker (filter and form). The time-entry list request sends `userId`,
`matterId`, `from`, `to`, `skip`, `take`. It never sends `clientId`.

Timekeepers come from `GET /api/v1/users?take=200` (seed: 25). Clients for the
picker come from `GET /api/v1/clients?take=200` (seed: 60). Matters for a chosen
client from `GET /api/v1/clients/{id}/matters?take=200`.

Inactive parties remain in the pickers, labelled "(inactive)", so SC-003 can
drive rule 5 and the inactive-timekeeper check from the UI.

**Rationale.** FR-003 forbids a client-id listing filter the listing does not
support. FR-021 requires read-only directories, not 009 screens. Hiding
inactive parties would make the refusing half of those rules unreachable from
the form and would look like the UI had invented an extra rule.

**Alternatives rejected.**

- *A numeric matter-id field and no picker.* Satisfies the API, fails SC-001.
- *N+1 fetch of every client's matters up front.* 60 extra calls to build a
  product directory. That is 009.
- *A new `GET /api/v1/matters`.* New endpoint, out of this spec.
- *`isActive=true` on the picker lists.* Quietly prevents the walkthrough from
  ever seeing those refusals.

---

## R6. Display names are a cache, not a join

**Decision.** Timekeeper names are resolved from the one users page (R5).
Matter (and through it, client) names are resolved with
`GET /api/v1/matters/{id}` for identifiers on the current listing page that are
not already in memory, then cached for the session. If a name is missing, the
row stays identifiable by `userId` / `matterId`. The first P3 cut is to skip
list-row matter fetches and keep names in the detail pane only.

**Rationale.** `TimeEntryDto` carries identifiers, not names. Inventing a
joined listing would be new API behaviour (FR-018). Per-page GETs are bounded
by `take` (≤ 100) and by cache hits.

**Alternatives rejected.**

- *Show only ids forever.* Legal under FR-002's MAY; worse for SC-001 than a
  cache.
- *Embed names in the time-entry DTO.* Schema/API change.

---

## R7. Duration is minutes on the wire, tenths on the screen

**Decision.** The form submits `durationMinutes` as a whole number, which is
what the service stores. The listing and detail **display** hours to one
decimal (`durationMinutes / 60`), which is a tenth of an hour whenever the
entry is legal. Detail also shows the minute figure so a reviewer can match
the JSON. The form does **not** refuse a value for failing the six-minute
increment, the 1,440 cap, or the daily cap — those refusals come back as
`violations[]` (R8). Required-field emptiness is the only local block.

**Rationale.** FR-010. A `step={6}` that prevents submit would hide rule 1's
service message, which is the thing this UI exists to show. Displaying tenths
satisfies the spec assumption that a billing operator can reconcile the figure
with invoice units.

**Alternatives rejected.**

- *Hours input that the UI multiplies by 60.* Easy to drift from whole minutes
  (0.11 h) and a second place that knows about tenths.
- *Client-side copies of rules 1–3 "for snappiness".* Two copies. P6.

---

## R8. The problem document is the refusal UI

**Decision.** On `400` with `title: "Domain rule violated"`, render every
element of `violations[]`: `detail` as the sentence, `rule` as the name. Do
not parse English to guess the field. Do not keep only the first item. On
`404`, show a missing-record state. On `401`, reuse 007's sign-in prompt and
keep the listing filters. Incomplete required fields never leave the browser
and are not labelled as a domain rule.

**Rationale.** Feature 005 already designed the problem document so a client
can act without parsing the sentence. The UI's job is to put that document on
the page. Dropping extras would undo "all violated rules are returned".

**Alternatives rejected.**

- *Map `rule` to a locally authored message.* A second wording, eventually
  wrong.
- *Treat every 400 as "invalid request".* Fails FR-010 and SC-003.

---

## R9. Coverage stays on the host and feature 005, not a new UI runner

**Decision.** No Playwright, no Node in CI. xUnit asserts: `GET /` is still
200 HTML without a bearer token; `GET /api/v1/time-entries` still returns 401
without a token. The twelve rule-pair tests remain feature 005's. The
walkthrough in [quickstart.md](./quickstart.md) is how SC-001–SC-004 are shown.

**Rationale.** P13. A Playwright stack would add Node to the pipeline for
forms whose refusals are already proven against real SQL Server. 007 made the
same call; 008's forms are not a reason to reverse it.

**Alternatives rejected.**

- *Playwright against Testcontainers.* Correct, and a second quality story the
  reviewer cannot run with `dotnet test`.
- *No host assertion that the time-entry collection is still closed.* Then
  "the page is open, the API is not" is only true for the rollup.

---

## R10. Mockup 04 is chrome and columns, not a product

**Decision.** Follow
[04-time-entries.png](../007-billing-operations-ui/mockups/04-time-entries.png)
for: navy sidebar, Time entries current, date / timekeeper / matter filters, a
paged table, a detail pane, Record time, Edit entry, captured rate as
read-only, billable as text (and optional icon, never colour alone).

Do not ship from that mockup: Overview, Settings, Clients / Matters /
Timekeepers as destinations, search, the four trend cards, realization,
draft/posted status, or a rate input.

**Rationale.** The mockup describes a product. The spec and the amended PRD
describe a thin consumer of five existing routes. Following the table and the
write actions makes SC-001/SC-002 possible; following the KPI rail would
invent figures the listing does not return (FR-018).

**Alternatives rejected.**

- *Build the mockup as drawn.* Repeats the P3 failure that split 007.
- *An unstyled table with no detail pane.* The user supplied a visual contract
  for this screen; ordinary CSS can match the listing without the product
  chrome.
