# Contract — Time Entry Operations UI (008)

The Time entries view is a same-origin consumer of the existing time-entry and
party routes. It does not add a route under `/api/v1`. The write/list contract
is [feature 005's](../../005-time-entries-and-rules/contracts/time-entry-endpoints.md);
the refusal contract is
[feature 005's domain rules](../../005-time-entries-and-rules/contracts/domain-rules.md).
This file is what the browser must do with them.

Registered in the host by the existing `app.MapDashboardFiles()`. Anonymous
HTML. The five time-entry routes and the party routes stay closed.

## Pages

| Path | Auth to load the page | Auth to load data |
| --- | --- | --- |
| `/` | None. The HTML and assets are static files | Bearer token, pasted into the sign-in field |
| `/#reports` | Same | Rollup, unchanged from 007 |
| `/#time-entries` | Same | Time-entry list / writes and party lookups |
| `/swagger` | Unchanged | — |
| `/health` | Unchanged | — |
| `/api/v1/time-entries` | Unchanged: 401 without a token | Unchanged |
| `/api/v1/users`, `/api/v1/clients`, `/api/v1/matters/{id}` | Unchanged: 401 without a token | Unchanged |

There is no `/clients`, `/matters`, `/timekeepers`, or `/settings` destination
in this feature (FR-001, SC-009). Choosing an existing timekeeper or matter
inside the time-entry form is not those destinations.

Visual contract:
[04-time-entries.png](../../007-billing-operations-ui/mockups/04-time-entries.png)
for the authenticated listing (table, filters, detail, Record / Edit). Sign-in
chrome remains 007's token card. What that mockup shows that this contract
refuses: [research.md R10](../research.md).

Unauthenticated `/` shows the sign-in chrome. After a token is stored, the
sidebar offers **Time entries** (current when the hash says so) and **Reports**.

## Listing request the UI makes

```
GET /api/v1/time-entries?from=2026-08-10&to=2026-08-13&skip=0&take=20
Authorization: Bearer <token>
```

Optional `userId` and `matterId`. No `clientId`. Not sent when the range is
incomplete or inverted.

## Record request the UI makes

```
POST /api/v1/time-entries
Authorization: Bearer <token>
```

```jsonc
{
  "userId": 1,
  "matterId": 1,
  "workDate": "2026-08-16",
  "durationMinutes": 6,
  "isBillable": true,
  "narrative": "Telephone call with client regarding status."
}
```

No `hourlyRateSnapshot`. A successful `201` body is shown; the captured rate
on it is displayed, never copied back into an input.

## Revise request the UI makes

```
PUT /api/v1/time-entries/{id}
Authorization: Bearer <token>
```

```jsonc
{
  "matterId": 1,
  "workDate": "2026-08-13",
  "durationMinutes": 12,
  "isBillable": true,
  "narrative": "Corrected narrative."
}
```

No `userId`. No rate. The timekeeper control is absent or disabled.

## Delete request the UI makes

```
DELETE /api/v1/time-entries/{id}
Authorization: Bearer <token>
```

Only after an explicit confirmation. `204` removes the row from the listing.
Cancel makes no request.

## Refusal rendering

A `400` problem document with `violations` is shown in full:

```jsonc
{
  "title": "Domain rule violated",
  "status": 400,
  "detail": "Duration 7 is not a positive multiple of 6 minutes.",
  "violations": [
    {
      "rule": "DurationIncrement",
      "offendingValue": "7",
      "detail": "Duration 7 is not a positive multiple of 6 minutes."
    }
  ]
}
```

Every array element is visible. The UI does not replace `detail` with a locally
authored sentence. Several elements mean several messages, one round trip.

## States the UI must expose

Mapped from [data-model.md](../data-model.md). Each state has a next action.

| Outcome | User-facing state | Next action |
| --- | --- | --- |
| Ready listing | Filters labelled; table of the current page; `total` is the match count | Change filters or page |
| Empty (`items: []`) | Explicit empty, not a blank table | Change filters |
| Incomplete / inverted range | Actionable validation; no fetch | Correct the dates |
| Missing required field | Field-associated message; no write | Fill the field |
| Domain refused | Every `violations[].detail`; success not claimed | Correct the values |
| Missing record | Explicit missing, not a crash | Return to the listing |
| 401 / missing token | Sign-in field; selected filters kept | Paste token, retry |
| Network / unavailable | Failed-request state; success not claimed | Retry |

Color is not the only signal for errors, empty vs data, or billable vs not
(FR-017).

## What the UI must not do

- Invent listing totals, period-over-period trends, realization, search, or
  draft/posted status.
- Send `clientId` on the listing, a rate on record or revise, or a timekeeper
  on revise.
- Re-implement the six domain rules (or the inactive-timekeeper check) as
  client-side limits that hide the problem document.
- Offer client, matter, or timekeeper create/edit screens.
- Expose stack traces, connection strings, or problem `traceId` values as the
  primary message.
- Require a command that is not already in the README quickstart in order to
  open `/#time-entries`.

## Host contract (so P18 is testable)

After `dotnet run --project src/LexTime.Api`:

| Request | Expected |
| --- | --- |
| `GET /` (no `Authorization`) | 200, HTML |
| `GET /health` | Unchanged |
| `GET /swagger` | Unchanged |
| `GET /api/v1/time-entries?from=2026-08-10&to=2026-08-13` without `Authorization` | 401 |
| `GET /api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13` without `Authorization` | 401 |
