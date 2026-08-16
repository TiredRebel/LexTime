# Quickstart — Billing Dashboard (007)

How to prove the weekly rollup is visible in a browser from the same two commands
that already start the API.

**Node is not required for this walkthrough.** It is required only when changing
the UI source under `web/` and regenerating `wwwroot`.

## Prerequisites

- Docker, running
- .NET SDK 9.0.x (`global.json` pins 9.0.317)

## Setup — still two commands

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

The script prints a development token. Copy it.

```powershell
dotnet run --project src/LexTime.Api
```

Open `http://localhost:5202/`. Swagger remains at `http://localhost:5202/swagger`.
Health remains at `http://localhost:5202/health`.

## Validation 1 — the headline is readable

1. Paste the token into the dashboard sign-in field.
2. The selected range should already show **2026-06-18** to **2026-08-13**.
3. Confirm a table of weeks and clients: billable hours, non-billable hours,
   billable amount, cumulative hours, prior-week change, standing — the columns
   in [mockups/03-weekly-billable-rollup.png](./mockups/03-weekly-billable-rollup.png).
   There is no daily chart and no Export control.
4. Identify the busiest client in a week (lowest standing number) and that its
   amount is a number, not a blank.
5. Change **Rows per page** between 20, 50, and 100, move to the next page, and
   confirm the row range and page number update without changing the summary
   totals.

This is SC-001's walkthrough. Field meanings are in
[data-model.md](./data-model.md); the JSON shape is in
[feature 003's rollup contract](../003-weekly-billable-rollup/contracts/rollup-endpoint.md).

## Validation 2 — empty is not zero, null is not zero

Keep the token.

**Empty.** Set the range to `2030-01-07` .. `2030-02-04` (the endpoint test's empty
future window) and apply. The dashboard must say there is no matching activity, not
show a table of zeros and not show the previous period's figures as current.

**Inverted.** Set `from` later than `to` and apply. The request must not go out. An
actionable message names the problem. Previous figures are not current.

**Prior-week comparison.** Return to 2026-06-18 .. 2026-08-13. On each client's
**first** week in that range, the prior-week change is unavailable for comparison,
not `0`. A later week for the same client may show a numeric change, including a
true zero if that is what the service returned.

Compare any one row against
`GET http://localhost:5202/api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13`
with the same token. The UI must not disagree with the JSON on hours, amount,
standing, or the null delta.

## Validation 3 — filter preserves standing

Still on 2026-06-18 .. 2026-08-13. Pick one client from the filter. Only that
client's rows remain. Its standing is still the number it had among **all** clients
that week in the unfiltered JSON, not `1` of `1`.

## Validation 4 — the page is open, the report is not

In a private window, open `http://localhost:5202/` **without** pasting a token.
The dashboard page loads. Applying a range without a token asks for sign-in and
does not display figures.

`GET http://localhost:5202/api/v1/reports/weekly-billable-rollup?from=2026-06-18&to=2026-08-13`
without `Authorization` still returns 401.

## Validation 5 — this slice has no other workflows

On the dashboard, there is no control that records time, registers a client, opens
a matter, or edits a timekeeper. Those are later specs. Swagger still has those
routes; the dashboard does not.

## Regenerating the UI (not part of the reviewer quickstart)

```powershell
cd web
npm ci
npm run build
```

`npm run build` replaces `src/LexTime.Api/wwwroot` from the fresh static export.
Then `dotnet run` serves the new files. A reviewer who does not change the UI
never runs this.
