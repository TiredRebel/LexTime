# Data Model — Party Directory UI (009)

**No schema change. No migration. No new endpoint.** Clients, matters, and
timekeepers are feature 006 and are unchanged. This feature adds browser views
of those records and a session that already holds the development token.

A migration or a new listing field appearing here would mean something has gone
wrong.

## What already exists, and what this feature does with it

The shapes are defined in
[feature 006's client contract](../006-clients-and-matters/contracts/client-endpoints.md)
and
[matter and timekeeper contract](../006-clients-and-matters/contracts/matter-and-timekeeper-endpoints.md).
The UI displays them. It does not recompute them.

### `ClientDto` — one table row and the detail pane

| Field | Shown as |
| --- | --- |
| `clientId` | Identity for select / correct, not a prominent column |
| `clientCode` | Code. Input on register only; read-only text on correct (R6) |
| `name` | Client name |
| `isActive` | Active or not, as text. Colour is optional and never the only signal |
| `createdAtUtc` | Registration time. Listing and detail |

There is no matter count, billed amount, or last-activity field on this DTO.
Those figures are not derived from extra requests either (FR-002).

### `MatterDto` — nested under the selected client

| Field | Shown as |
| --- | --- |
| `matterId` | Identity for select / correct |
| `clientId` | Owning client. Displayed, never an input on correct |
| `matterNumber` | Number. Input on open only; read-only text on correct (R6) |
| `name` | Matter name |
| `isBillableByDefault` | Default billable, as text. Input on open and correct |
| `isActive` | Active or not, as text |

There is no practice area, responsible timekeeper, or recent-entry list.

### `TimekeeperDto` — one roster row and the read-only pane

| Field | Shown as |
| --- | --- |
| `userId` | Identity for select |
| `fullName` | Timekeeper |
| `email` | Email |
| `defaultHourlyRate` | Current rate, read-only. Never an input |
| `isActive` | Active or not, as text |

There is no role, no created timestamp on this DTO, and no recent-time feed.
The pane is labelled read-only (FR-008, FR-016).

### Listing envelopes

| Field | This feature |
| --- | --- |
| `skip`, `take` | Echoed from the page controls after the API clamps them |
| `total` | Matching count for the current filter. Footer copy and page count use this, not `items.length` |
| `items` | Empty list → empty success, not an error, not a previous page |

### Listing requests the UI sends

**Clients**

| Query | This feature |
| --- | --- |
| `isActive` | Omitted for All; `true` or `false` when restricted (R4) |
| `skip` | `(page - 1) * take` |
| `take` | 20, 50, or 100 (R3) |

**Timekeepers**

| Query | This feature |
| --- | --- |
| `skip`, `take` | Same paging. **No `isActive`** — the listing does not support it |
| `role` / search | **Not sent** |

**Matters**

| Query | This feature |
| --- | --- |
| path `clientId` | The selected client. Never a firm-wide call |
| `skip`, `take` | Same paging. **No `isActive`** |

### Register body the UI sends

`clientCode`, `name`. **No `isActive`.** The created client is active.

### Correct-client body the UI sends

`name`, `isActive`. **No `clientCode`.**

### Open-matter body the UI sends

`matterNumber`, `name`, `isBillableByDefault`. **No `clientId` in the body**
(it is in the route). **No `isActive`.**

### Correct-matter body the UI sends

`name`, `isBillableByDefault`, `isActive`. **No `matterNumber`. No `clientId`.**

## Uniqueness conflict (not stored)

A `409` problem document. Displayed, not authored.

| Field | This feature |
| --- | --- |
| `title`, `detail` | Shown as the sentence |
| `conflictingField` | `clientCode` or `matterNumber` |
| `conflictingValue` | The value the operator submitted |

A missing parent when opening a matter is `404`, not `409`. Empty or
whitespace-only fields are `400` if they leave the browser, or a local
required-field block if they do not. Those three must not look like each
other.

## View state (this feature only)

Not stored. Not a domain entity. User Story 2 and FR-005 require these to stay
distinct.

| State | When | Must not look like |
| --- | --- | --- |
| Loading | A list or write is in flight | The previous page or a previous write, unlabelled |
| Ready | `200` with one or more items | — |
| Empty | `200` with `items: []` | An error, or the previous page, or a missing parent |
| Missing | `404` on get / correct | An empty matter list, or a blank success |
| Missing parent | `404` on open-matter | A uniqueness conflict |
| Conflict | `409` with field and value | A successful save, or a malformed-field message |
| Malformed | Local required-field block, or `400` | A uniqueness conflict |
| Unauthenticated | No token, or the service returned 401 | A blank error page, or a listing |
| Unavailable | Network failure or non-problem 5xx | A successful empty listing |

There is no confirming-delete state. Deactivation is a correct of `isActive`.

## UI session

| Field | Where | Notes |
| --- | --- | --- |
| Bearer token | `sessionStorage` | Same key as 007. Cleared on 401. Not a new IdP |
| Destination | `location.hash` | `#clients`, `#timekeepers`, `#time-entries`, or `#reports` (R2) |
| Client status filter | Form control | All / Active / Inactive. Initial All (R4). Survives a sign-in prompt |
| Selected client | Component state | Drives detail and the nested matter list. Hash promotion is P3 cut 2 |
| Client page / size | Component state | Size is 20, 50, or 100; page resets to 1 when filter or size changes |
| Matter page / size | Component state | Resets to 1 when the selected client changes |
| Selected matter | Component state | Drives the matter detail / correct form |
| Timekeeper page / size | Component state | Same sizes; no status query |
| Selected timekeeper | Component state | Drives the read-only pane |

## Validation rules (browser, before fetch or write)

Mirrored so empty required fields do not leave the form:

- register: code and name present after trim
- open matter: number and name present after trim; a client must already be
  selected
- correct: name present after trim

The service remains the source of truth for uniqueness (including
case-insensitive codes), missing parents, immutability, and the absence of
timekeeper writes. The browser does not GET-then-POST to predict a 409 (R5).

## State transitions (write)

```text
register / open form ready
  → submit
      → 201: listing refreshes; new row selected; form closes
      → 409: form stays open; field and value shown; listing unchanged
      → 404 (open): missing-parent state
      → 400: malformed message; not labelled as a conflict
      → 401: sign-in prompt; directory, client, and filters kept
correct form ready
  → submit
      → 200: record refreshed; code / number / owner unchanged
      → 404: missing-record state
      → 401: sign-in prompt; selection kept
```

Deactivating a client does not rewrite the nested matter flags. The UI
re-reads the matter page after a client correct so that fact is visible
rather than inferred.
