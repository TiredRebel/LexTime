# Data Model — Billing Dashboard (007)

**No schema change. No migration. No new endpoint.** The weekly rollup's records
are feature 003's and are unchanged. This feature adds a browser view of those
records and a session that holds the token and the selected period.

A migration or a new report field appearing here would mean something has gone
wrong.

## What already exists, and what this feature does with it

The shapes below are defined in
[feature 003's data model](../003-weekly-billable-rollup/data-model.md) and
returned by
[GET /api/v1/reports/weekly-billable-rollup](../003-weekly-billable-rollup/contracts/rollup-endpoint.md).
The dashboard displays them. It does not recompute them.

### `WeeklyBillableRollupQuery` — the request the dashboard sends

| Field | This feature |
| --- | --- |
| `From` | Inclusive start, from the date controls. Required before fetch (R6) |
| `To` | Inclusive end, same |
| `ClientId` | **Not sent.** The filter is a display restriction of the fetched period (R5) |

### `WeeklyBillableRollupRow` — one table row

| Field | Shown as |
| --- | --- |
| `IsoYear`, `IsoWeek`, `WeekStartDate` | Week identity. Enough that a multi-week range is readable |
| `ClientCode`, `ClientName` | Who the row is for. Also the filter picker's labels |
| `ClientId` | Filter value, not a prominent column |
| `BillableHours`, `NonBillableHours`, `BillableAmount` | The figures. Zero is a number, including `0.0` |
| `CumulativeBillableHours` | Running total inside the selected period |
| `HoursDeltaVsPriorWeek` | Numeric change, **or** an explicit "no comparison" when null. Never coalesced to zero (R9) |
| `ClientRankInWeek` | Standing among all clients that week. Unchanged by the client filter (R5) |

### `WeeklyBillableRollupResponse` — the envelope

| Field | This feature |
| --- | --- |
| `From`, `To` | Echoed next to the controls so the figures are labelled as a report of that period |
| `Rows` | Empty list → empty state, not an error, not a table of zeros |

## View state (this feature only)

Not stored. Not a domain entity. The dashboard's job is to make these distinct
(User Story 2).

| State | When | Must not look like |
| --- | --- | --- |
| Loading | A request is in flight | The previous period's figures, unlabelled |
| Ready | `200` with one or more rows | — |
| Empty | `200` with no rows, or a client filter that matches none | Zero billable, or an error |
| Zero-billable | Rows exist, billable hours and amount are zero, non-billable may not be | Empty |
| Blocked range | Incomplete or inverted dates | Unavailable service |
| Unauthenticated | No token, or the service returned 401 | A blank error page, or figures |
| Unavailable | Network failure or non-problem 5xx | A successful empty report |

## UI session

| Field | Where | Notes |
| --- | --- | --- |
| Bearer token | `sessionStorage` | Pasted from `Initialize-LocalDb.ps1`. Cleared on 401. Not a new IdP (R3) |
| From / To | Form controls | Initial values 2026-06-18 .. 2026-08-13 (R4). Survive a sign-in prompt |
| Client filter | Form control | `all`, or one `clientId` from the last ready rows. Survives a sign-in prompt |
| Page / page size | Component state | Applied after the client filter. Size is 20, 50, or 100; page resets to 1 when the range, client, or size changes |

No other client, matter, timekeeper, or time-entry fields exist in this slice.

## Validation rules (browser, before fetch)

Mirrored from the endpoint so inverted ranges do not leave the form (R6):

- both dates present
- `From` must not be later than `To`

The service remains the source of truth for everything else, including standing
and the meaning of a null prior-week delta.
