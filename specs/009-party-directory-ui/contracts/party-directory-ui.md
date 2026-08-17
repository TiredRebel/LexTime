# Contract — Party Directory UI (009)

The Clients and Timekeepers views are a same-origin consumer of the existing
party routes. They do not add a route under `/api/v1`. The write/list contract
is [feature 006's](../../006-clients-and-matters/contracts/client-endpoints.md)
and
[matter-and-timekeeper-endpoints.md](../../006-clients-and-matters/contracts/matter-and-timekeeper-endpoints.md).
This file is what the browser must do with them.

Registered in the host by the existing `app.MapDashboardFiles()`. Anonymous
HTML. The party routes stay closed.

## Pages

| Path | Auth to load the page | Auth to load data |
| --- | --- | --- |
| `/` | None. The HTML and assets are static files | Bearer token, pasted into the sign-in field |
| `/#reports` | Same | Rollup, unchanged from 007 |
| `/#time-entries` | Same | Time-entry list / writes, unchanged from 008 |
| `/#clients` | Same | Client list / writes and that client's matters |
| `/#timekeepers` | Same | Timekeeper list / get only |
| `/swagger` | Unchanged | — |
| `/health` | Unchanged | — |
| `/api/v1/clients` | Unchanged: 401 without a token | Unchanged |
| `/api/v1/users` | Unchanged: 401 without a token | Unchanged |

There is no `/#matters`, `/settings`, or `/overview` destination in this
feature (FR-001, SC-011). Matters are listed on the selected client (R2).
008's picker calls are not these destinations.

Visual contract:

- [05-clients.png](../../007-billing-operations-ui/mockups/05-clients.png)
  for the authenticated client listing (table, status filter, detail, Add
  client). Matter columns on that mockup that the DTO does not carry are
  omitted.
- [06-matters.png](../../007-billing-operations-ui/mockups/06-matters.png)
  for the nested matter table and detail under one client, not as a sidebar
  destination.
- [07-timekeepers.png](../../007-billing-operations-ui/mockups/07-timekeepers.png)
  for the roster and the read-only pane.

Sign-in chrome remains 007's token card. What those mockups show that this
contract refuses: [research.md R9](../research.md).

Unauthenticated `/` shows the sign-in chrome. After a token is stored, the
sidebar offers **Reports**, **Time entries**, **Clients**, and **Timekeepers**.

## Listing requests the UI makes

```
GET /api/v1/clients?skip=0&take=20
Authorization: Bearer <token>
```

Optional `isActive=true` or `isActive=false`. Omitted means All. Never a
search term.

```
GET /api/v1/clients/{clientId}/matters?skip=0&take=20
Authorization: Bearer <token>
```

Only after a client is selected. Not sent as a firm-wide list.

```
GET /api/v1/users?skip=0&take=20
Authorization: Bearer <token>
```

No `isActive`. No role.

## Register request the UI makes

```
POST /api/v1/clients
Authorization: Bearer <token>
```

```jsonc
{ "clientCode": "WALK", "name": "Walkthrough Holdings" }
```

No `isActive`. A successful `201` body is shown as active. The code on it is
displayed, never copied back into a correction input.

## Correct-client request the UI makes

```
PUT /api/v1/clients/{clientId}
Authorization: Bearer <token>
```

```jsonc
{ "name": "Walkthrough Holdings Ltd", "isActive": false }
```

No `clientCode`.

## Open-matter request the UI makes

```
POST /api/v1/clients/{clientId}/matters
Authorization: Bearer <token>
```

```jsonc
{ "matterNumber": "001", "name": "Walkthrough — Phase 1", "isBillableByDefault": true }
```

No `clientId` in the body. No `isActive`.

## Correct-matter request the UI makes

```
PUT /api/v1/matters/{matterId}
Authorization: Bearer <token>
```

```jsonc
{ "name": "Walkthrough — Phase 1 (revised)", "isBillableByDefault": true, "isActive": false }
```

No `matterNumber`. No `clientId`.

## Timekeeper requests the UI makes

`GET /api/v1/users` and `GET /api/v1/users/{userId}` only. The UI never
sends `POST` or `PUT` to those routes.

## Conflict rendering

A `409` problem document is shown in full:

```jsonc
{
  "title": "Client code already in use",
  "status": 409,
  "detail": "A client with code 'ACME' already exists.",
  "conflictingField": "clientCode",
  "conflictingValue": "ACME"
}
```

The operator sees the sentence and the field and value. The UI does not
replace `detail` with a locally authored sentence. Registering `acme` when
`ACME` exists is the same document; the UI does not special-case case.

A matter-number conflict uses `conflictingField: "matterNumber"` and a detail
that names the client. Reusing `001` under a *different* client is not a
conflict and must succeed.

## States the UI must expose

Mapped from [data-model.md](../data-model.md). Each state has a next action.

| Outcome | User-facing state | Next action |
| --- | --- | --- |
| Ready listing | Filters labelled; table of the current page; `total` is the match count | Change filter or page |
| Empty (`items: []`) | Explicit empty, not a blank table | Change filter, or open a matter if this is a client with none |
| Missing required field | Field-associated message; no write | Fill the field |
| Conflict `409` | Sentence plus field and value; success not claimed | Choose another code or number |
| Missing parent `404` on open | Explicit missing client, not a conflict | Return to Clients |
| Missing record `404` | Explicit missing, not a crash | Return to the listing |
| 401 / missing token | Sign-in field; selected directory, client, and filters kept | Paste token, retry |
| Network / unavailable | Failed-request state; success not claimed | Retry |

Color is not the only signal for errors, empty vs data, or active vs not
(FR-023). Timekeeper detail includes an explicit read-only label.

## What the UI must not do

- Invent listing totals, count cards, billed or unbilled amounts, search,
  roles, practice areas, or recent-entry widgets.
- Send `isActive` on timekeeper or matter listings, a code on client correct,
  a number or client id on matter correct, or any write to `/api/v1/users`.
- Pre-check uniqueness with a GET before POST.
- Offer a firm-wide matters table, a Matters sidebar destination, delete,
  merge, or renumber.
- Expose stack traces, connection strings, or problem `traceId` values as the
  primary message.
- Require a command that is not already in the README quickstart in order to
  open `/#clients` or `/#timekeepers`.

## Host contract (so P18 is testable)

After `dotnet run --project src/LexTime.Api`:

| Request | Expected |
| --- | --- |
| `GET /` (no `Authorization`) | 200, HTML |
| `GET /health` | Unchanged |
| `GET /swagger` | Unchanged |
| `GET /api/v1/clients` without `Authorization` | 401 |
| `GET /api/v1/users` without `Authorization` | 401 |
| `GET /api/v1/time-entries?from=2026-08-10&to=2026-08-13` without `Authorization` | 401, unchanged from 008 |
| `GET /api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13` without `Authorization` | 401, unchanged from 007 |
