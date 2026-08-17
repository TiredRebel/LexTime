# Research — Party Directory UI (009)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected.
Uniqueness, immutability, and the read-only timekeeper surface are not
re-decided here; they live in
[feature 006's contracts](../006-clients-and-matters/contracts/client-endpoints.md)
and
[matter-and-timekeeper-endpoints.md](../006-clients-and-matters/contracts/matter-and-timekeeper-endpoints.md).
The shell, token session, and static export are not re-decided here; they live
in [feature 007's research](../007-billing-operations-ui/research.md) (R1–R3, R8).
Hash destinations as a pattern live in
[feature 008's R2](../008-time-entry-ui/research.md).

The PRD permission that unblocks this spec is `a829911` on `main`
("Permit a thin operations UI now that the API is complete.").

---

## R1. Same origin, same two commands, same token field

**Decision.** Keep 007's static export under `src/LexTime.Api/wwwroot`, served by
the existing `dotnet run`. Keep the development-token paste and
`sessionStorage` key `lextime.development-token`. Node is still not on the
reviewer path. Regenerating `wwwroot` remains a developer step when `web/`
changes.

**Rationale.** Constitution P18 and FR-026. A second origin, a login endpoint,
or an `npm` step on `dotnet build` would fail the same gates 007 and 008
already passed. Party directories are another consumer of the same session,
not a new auth model (FR-025).

**Alternatives rejected.**

- *`next start` plus CORS.* Third command, second origin, new API behaviour.
- *A new `/login` or `/token` route.* Looks like starting an IdP (§2.2).
- *A second static site at `/clients`.* Extra host wiring for no new meaning
  once R2 is taken.

---

## R2. In-shell views at `/`; matters live on the selected client

**Decision.** One exported page at `/`. The navy sidebar gains **Clients** and
**Timekeepers**, and keeps **Reports** and **Time entries**. Selected
destination is `location.hash`: `#clients`, `#timekeepers`, `#time-entries`,
or `#reports` (default). There is **no Matters sidebar item** and no
`#matters` destination. Selecting a client on Clients shows that client's
matters in the same view. Opening a matter is always under that client.

**Rationale.** FR-001 requires Clients and Timekeepers destinations and
requires matters to be shown for one client at a time. It does not require a
third destination. Feature 006 has no firm-wide matter list
(`GET /api/v1/clients/{id}/matters` only). A Matters nav item would either
invent `GET /api/v1/matters` (P2) or be a second "pick a client" screen that
duplicates Clients. Nesting is also the P3 save: two new views, not three,
which is why timekeepers stay in this spec (R7).

Refresh keeps the destination. Selected client stays in component state
unless later promoted into the hash (P3 cut 2). No `MapFallbackToFile`.

**Alternatives rejected.**

- *A real `/clients` route plus fallback middleware.* Same wall 007's R8 and
  008's R2 already named.
- *A Matters destination that first asks for a client.* Legal under the spec's
  planning note, and a whole extra empty state for no new API meaning.
- *N+1 fetch of every client's matters to build a firm-wide table.* Product
  directory, forbidden by FR-007.

**Trade-off (P19).** The mockup sidebar shows Matters. The address bar and
the IA do not. The operator still reaches every matter the API can list,
from the client it belongs to.

---

## R3. Directories are paged by the service, never by the browser

**Decision.** `GET /api/v1/clients` and `GET /api/v1/users` and
`GET /api/v1/clients/{id}/matters` with `skip` and `take`. Page sizes are
**20, 50, and 100** — the same choices as 007/008, all under the API's
maximum of 200. `skip = (page - 1) * take`. The footer shows the matching
`total`, not the page length. Changing status filter or page size resets to
page 1. A client change resets the matter page to 1.

**Rationale.** FR-004. The seed is 60 clients and ~220 matters. That is small
enough that `take=200` would "work" and still be the wrong habit: 008 already
taught the shell to page, and a later seed increase must not turn these
screens into an unpaged dump. Timekeepers are 25 rows; they still page so the
control is one pattern.

**Alternatives rejected.**

- *`take=200` always, no pager.* Legal today, silently wrong the day the
  directory exceeds one page, and inconsistent with Time entries.
- *Browser-side filter of one fetched page.* Hides rows that exist on later
  pages and pretends the service filtered them.
- *A new unpaged list endpoint.* New API behaviour this spec is not allowed
  to add.

---

## R4. Client listing opens on all statuses

**Decision.** On first load of Clients the status filter is **All** (`isActive`
omitted), shown as a labelled control the user can change to Active or
Inactive. Timekeeper and matter listings send no status query — the contract
has none — and show the `isActive` flag as a column.

**Rationale.** FR-003. Opening on active-only would hide the seeded inactive
clients that exist specifically so rule 5 and deactivation can be seen. A
silent default that hid them would fail the walkthrough and look like the UI
had invented a rule. Timekeeper/matter "Active only" dropdowns would be a
filter the listing does not support (spec assumption, FR-007).

**Alternatives rejected.**

- *Active-only as the initial filter.* Cleaner table; hides the interesting
  seed rows.
- *Client-side active filter on timekeepers.* Same trap as paging a subset of
  one page.

---

## R5. The 409 problem document is the conflict UI

**Decision.** On `409` with `conflictingField` and `conflictingValue`, render
`title` / `detail` and the field and value. Do not parse English to guess the
field. Do not look up the code before POST. On `404`, show missing-record
(or missing-parent when opening a matter). On `400` for empty or
whitespace-only fields, show the service message if a write went out;
incomplete required fields may also be blocked locally and are not labelled
as a uniqueness conflict. On `401`, reuse 007's sign-in prompt and keep the
selected directory, client, and filters.

**Rationale.** Feature 006 already designed the conflict document so a caller
can choose another code without reading a storage exception. A check-then-insert
in the browser is the same race 006's research R2 rejected on the server.
Case-insensitive collision (`ACME` vs `acme`) is a service fact; the UI must
not special-case it into a different message.

**Alternatives rejected.**

- *Map `conflictingField` to a locally authored sentence.* A second wording,
  eventually wrong.
- *Treat every 409 as "already exists".* Drops which field and which value
  (FR-011, SC-003).
- *GET-then-POST to "avoid" 409.* Race, extra round trip, and hides the
  document this UI exists to show.

---

## R6. Immutable fields are absent, not disabled

**Decision.** Register sends `clientCode` and `name` only. Open-matter sends
`matterNumber`, `name`, and `isBillableByDefault` only. Correct-client sends
`name` and `isActive` only. Correct-matter sends `name`, `isBillableByDefault`,
and `isActive` only. The correction UI shows code / number / owning client as
read-only text, not as inputs. There is no delete control.

**Rationale.** FR-014 / FR-017 and 006 FR-011: the update request must not
carry the immutable field, so the change cannot be attempted rather than
being attempted and ignored. A disabled input still looks like a field a
clever caller might send. Timekeeper detail has no form at all (FR-016).

**Alternatives rejected.**

- *Disabled code input that is omitted on submit.* Looks editable; fails
  SC-010's "not offered".
- *A Close button that DELETEs.* No such route; deactivation is the domain's
  answer.

---

## R7. Timekeepers stay in this spec

**Decision.** Do not split a 010. Timekeepers is a paged list and a read-only
detail pane on the same listing chrome as Clients, with zero write forms.
Matters nested under Clients (R2) is what makes the evening fit. The spec's
split valve remains: if implementation still overruns after P3 cuts 1 and 2,
stop and specify 010 rather than shipping a half of this spec.

**Rationale.** P3. A dedicated 010 that only lists 25 seeded people would
repeat shell wiring, paging, host 401 coverage, and `wwwroot` regeneration
for a screen that adds no new rule. 007 was split because dashboard + time
entries + parties would not fit; parties + a nested matter list is the size
008 already delivered for one resource with forms.

**Alternatives rejected.**

- *Split now "to be safe".* Three more Spec Kit commits and a merge for a
  table. That is process theatre, not a P3 save.
- *Drop Timekeepers from the sidebar and leave US1 scenarios 6–7 unbuilt.*
  An unplanned spec change. The valve says split, not silently omit.

---

## R8. Coverage stays on the host and feature 006, not a new UI runner

**Decision.** No Playwright, no Node in CI. xUnit asserts: `GET /` is still
200 HTML without a bearer token; `GET /api/v1/clients` and `GET /api/v1/users`
still return 401 without a token. Uniqueness pairs, immutability, cascade
absence, and "no POST/PUT on timekeepers" remain feature 006's. The
walkthrough in [quickstart.md](./quickstart.md) is how SC-001–SC-005 are shown.

**Rationale.** P13. A Playwright stack would add Node to the pipeline for
forms whose collisions are already proven against real SQL Server. 007 and
008 made the same call.

**Alternatives rejected.**

- *Playwright against Testcontainers.* Correct, and a second quality story the
  reviewer cannot run with `dotnet test`.
- *No host assertion that the party collections are still closed.* Then "the
  page is open, the API is not" is only true for the rollup and time entries.

---

## R9. Mockups 05–07 are chrome and columns, not a product

**Decision.** Follow the mockups for: navy sidebar, Clients or Timekeepers
current, a status control on Clients, a paged table, a detail pane, Add
client / Open matter, Active as text (and optional icon, never colour alone),
"Read-only" on the timekeeper pane.

Do not ship from those mockups: Overview, Settings, a Matters nav item,
search, Active/Inactive/Total count cards, billed this month, unbilled time,
last activity, roles, partners/associates counts, practice areas, responsible
timekeepers, recent time entries, or a rate editor.

A text control **View time entries** that only changes the hash to
`#time-entries` is the whole of FR-027 unless P3 cut 1 removes even that.
It must not render a recent-entries list.

**Rationale.** The mockups describe a product. The spec and the amended PRD
describe a thin consumer of existing party routes. Following the table and
the write actions makes SC-001/SC-002 possible; following the KPI rail would
invent figures the listing does not return (FR-002, FR-008, FR-024).

**Alternatives rejected.**

- *Build the mockups as drawn.* Repeats the P3 failure that split 007, and
  needs endpoints 006 does not have.
- *An unstyled table with no detail pane.* The user supplied a visual
  contract; ordinary CSS can match the listing without the product chrome.

---

## R10. 008 pickers stay on `party-lookups.ts`; directories get `parties-api.ts`

**Decision.** Leave `web/app/party-lookups.ts` as 008 left it: `take=200`
picker pages, matter cache, `TimeEntryRequestError`. New `parties-api.ts`
owns paged directory reads (`skip`/`take`/`total`), POST/PUT, and a
`PartyRequestError` whose kinds include `conflict` with `conflictingField`
and `conflictingValue`. Client DTOs in the directory include `createdAtUtc`;
the picker type does not have to grow that field.

**Rationale.** 008's plan said the lookup module exists so 009 can add
directory screens *without rewriting the listing*. Rewriting pickers onto
paged directory state would couple Time entries to Clients pagination and
risk a `take=20` picker that hides most matters. Two modules with one
duplicated GET is cheaper than one module with two jobs.

**Alternatives rejected.**

- *One module that both pages and fills pickers.* Pickers would inherit
  directory page size or directories would inherit `take=200`.
- *Change 008 pickers to call the new paged helper with `take=200`.* Fine
  later; not required to ship 009, and it is an 008 behaviour change this
  spec does not own.
