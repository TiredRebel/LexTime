# Contract — Billing Dashboard UI (007)

The dashboard is a same-origin consumer of the existing weekly rollup. It does
not add a route under `/api/v1`. The report contract is
[feature 003's](../../003-weekly-billable-rollup/contracts/rollup-endpoint.md);
this file is what the browser must do with it.

Registered in the host by `app.MapDashboardFiles()` (P21, R8). Anonymous. The
rollup route stays closed.

## Pages

| Path | Auth to load the page | Auth to load figures |
| --- | --- | --- |
| `/` | None. The HTML and assets are static files | Bearer token, pasted into the sign-in field |
| `/swagger` | Unchanged | — |
| `/health` | Unchanged | — |
| `/api/v1/reports/weekly-billable-rollup` | Unchanged: 401 without a token | Unchanged |

There is no `/entries`, `/clients`, `/matters`, or `/timekeepers` in this feature
(FR-001, SC-007).

Visual contract: [mockups/03-weekly-billable-rollup.png](../mockups/03-weekly-billable-rollup.png)
for the authenticated rollup, [mockups/01-sign-in.png](../mockups/01-sign-in.png)
for the token card (token field, not password). Layout tokens and what is *not*
built from the set: [mockups/README.md](../mockups/README.md).

Unauthenticated `/` shows the sign-in chrome. After a token is stored it shows
the rollup: navy sidebar with Reports current, header with visible from/to and
client filter, table columns matching the rollup row (client, week, billable
hours, non-billable, amount, cumulative, delta, rank). Optional summary cards
may total those rows; they must not invent a vs-prior-period percentage. The
table pages the already returned, client-filtered rows at 20, 50, or 100 per
page without adding pagination parameters to the request.

## Report request the dashboard makes

```
GET /api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13
Authorization: Bearer <token>
```

- `from` and `to` are `YYYY-MM-DD`, inclusive, required.
- `clientId` is **not** sent (R5).
- The request is **not** sent when the range is incomplete or inverted (R6).

## Dashboard states the UI must expose

Mapped from [data-model.md](../data-model.md). Each state has a next action.

| Outcome | User-facing state | Next action |
| --- | --- | --- |
| Ready | Period labelled; table of rows; figures match the JSON | Change range or filter |
| Empty (`rows: []`, or filter matches none) | Explicit empty, not a blank table | Change range or clear filter |
| Zero billable on a row | `0` (or `0.0`) for hours/amount; non-billable still shown | — |
| `hoursDeltaVsPriorWeek: null` | Text equivalent of "no comparison", never `0` | — |
| Incomplete / inverted range | Actionable validation; no fetch | Correct the dates |
| 401 / missing token | Sign-in field; selected dates kept | Paste token, retry |
| Network / unavailable | Failed-request state; success not claimed | Retry |

Color is not the only signal for errors, empty vs zero, or a missing comparison
(FR-011).

## What the dashboard must not do

- Invent rows, totals, standings, or prior-week deltas.
- Recompute `clientRankInWeek` among the filtered rows.
- Offer time-entry, client, matter, or timekeeper create/edit actions.
- Expose stack traces, connection strings, or problem `traceId` values as the
  primary message.
- Require a command that is not already in the README quickstart in order to
  open `/`.

## Host contract (so P18 is testable)

After `dotnet run --project src/LexTime.Api`:

| Request | Expected |
| --- | --- |
| `GET /` (no `Authorization`) | 200, HTML |
| `GET /health` | Unchanged |
| `GET /swagger` | Unchanged |
| `GET /api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13` without `Authorization` | 401 |
