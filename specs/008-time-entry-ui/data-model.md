# Data Model — Time Entry Operations UI (008)

**No schema change. No migration. No new endpoint.** Time entries, clients,
matters, and timekeepers are features 005 and 006 and are unchanged. This
feature adds a browser view of those records and a session that already holds
the development token.

A migration or a new listing field appearing here would mean something has gone
wrong.

## What already exists, and what this feature does with it

The write/list shapes are defined in
[feature 005's endpoint contract](../005-time-entries-and-rules/contracts/time-entry-endpoints.md)
and
[feature 005's domain rules](../005-time-entries-and-rules/contracts/domain-rules.md).
Party shapes are feature 006's. The UI displays them. It does not recompute
them.

### `TimeEntryDto` — one table row and the detail pane

| Field | Shown as |
| --- | --- |
| `timeEntryId` | Identity for select / revise / delete, not a prominent column |
| `workDate` | Work date |
| `narrative` | Narrative |
| `userId` | Timekeeper. Display name from the users page when cached (R6) |
| `matterId` | Matter. Display name from `GET /matters/{id}` when cached (R6); otherwise the identifier |
| `durationMinutes` | Hours to one decimal (`minutes / 60`) in the table; hours and minutes in detail (R7) |
| `isBillable` | Billable or not, as text. Colour is optional and never the only signal |
| `hourlyRateSnapshot` | Captured rate, read-only. Never an input |
| `createdAtUtc`, `updatedAtUtc` | Detail pane only. No draft/posted status is derived from them |

There is no client id on this DTO. A client name, when shown, comes from the
resolved matter's `clientId` and the clients page, not from a listing filter.

### `TimeEntryPage` — the listing envelope

| Field | This feature |
| --- | --- |
| `skip`, `take` | Echoed from the page controls after the API clamps them |
| `total` | Matching count for the current filters. Footer copy and page count use this, not `items.length` |
| `items` | Empty list → empty success, not an error, not a previous page |

### Listing request the UI sends

| Query | This feature |
| --- | --- |
| `from`, `to` | Inclusive work dates from the date controls. Required before fetch (mirror of 007 R6 for inverted/incomplete ranges) |
| `userId` | Optional. Omitted when the timekeeper filter is "all" |
| `matterId` | Optional. Omitted when the matter filter is "all" |
| `skip` | `(page - 1) * take` |
| `take` | 20, 50, or 100 (R3) |
| `clientId` | **Not sent** (R5) |

### Record body the UI sends

`userId`, `matterId`, `workDate`, `durationMinutes`, `isBillable`, `narrative`.
**No rate.**

### Revise body the UI sends

`matterId`, `workDate`, `durationMinutes`, `isBillable`, `narrative`.
**No `userId`. No rate.** The timekeeper shown in the form is read-only.

## View state (this feature only)

Not stored. Not a domain entity. User Story 2 and FR-005 require these to stay
distinct.

| State | When | Must not look like |
| --- | --- | --- |
| Loading | A list or write is in flight | The previous page or a previous write, unlabelled |
| Ready | `200` with one or more items | — |
| Empty | `200` with `items: []` | An error, or the previous page |
| Blocked range | Incomplete or inverted dates | Unavailable service |
| Domain refused | `400` with `violations[]` | A successful save, or a generic "invalid request" |
| Missing | `404` on get / revise / delete | A blank success |
| Unauthenticated | No token, or the service returned 401 | A blank error page, or a listing |
| Unavailable | Network failure or non-problem 5xx | A successful empty listing |
| Confirming delete | Operator asked to remove a visible entry | An already-deleted row |

## UI session

| Field | Where | Notes |
| --- | --- | --- |
| Bearer token | `sessionStorage` | Same key as 007. Cleared on 401. Not a new IdP |
| Destination | `location.hash` | `#time-entries` or `#reports` (R2) |
| From / To | Form controls | Initial listing values 2026-08-10 .. 2026-08-13 (R4). Survive a sign-in prompt |
| Timekeeper filter | Form control | `all`, or one `userId` |
| Matter picker client | Form control | Loads matters. **Not** a listing query parameter |
| Matter filter | Form control | `all`, or one `matterId` |
| Page / page size | Component state | Size is 20, 50, or 100; page resets to 1 when filters or size change |
| Selected entry | Component state | Drives the detail pane |
| Name cache | Component state | Timekeepers from one users page; matters from per-id GETs (R6) |

## Validation rules (browser, before fetch or write)

Mirrored so inverted ranges and empty required fields do not leave the form:

- listing: both dates present, `from` not later than `to`
- record / revise: timekeeper (record only), matter, work date, duration, and
  narrative present

The service remains the source of truth for duration increment, duration
maximum, daily maximum, backdating window, active matter/client, active
timekeeper, captured rate, and missing parties.

## State transitions (write)

```text
form ready
  → submit
      → 201 / 200: listing refreshes; captured rate visible; form closes
      → 400 violations[]: form stays open; every detail shown; listing unchanged
      → 404: missing-record state
      → 401: sign-in prompt; filters kept
delete confirm
  → confirm → 204: row gone
  → cancel  → row remains
  → 404     → missing-record state
```
