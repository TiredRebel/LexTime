# Quickstart — Time Entry Operations UI (008)

How to prove recorded time is operable in a browser from the same two commands
that already start the API.

**Node is not required for this walkthrough.** It is required only when changing
the UI source under `web/` and regenerating `wwwroot`.

Field meanings are in [data-model.md](./data-model.md). The JSON the UI must
not disagree with is in
[feature 005's endpoint contract](../005-time-entries-and-rules/contracts/time-entry-endpoints.md).

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

Open `http://localhost:5202/`. Swagger remains at
`http://localhost:5202/swagger`. Health remains at
`http://localhost:5202/health`.

## Validation 1 — find recorded time

1. Paste the token into the sign-in field.
2. Choose **Time entries** in the sidebar (or open
   `http://localhost:5202/#time-entries`).
3. The selected range should already show **2026-08-10** to **2026-08-13**.
4. Confirm a bounded table: work date, narrative, timekeeper, matter, duration
   as tenths of an hour, billable flag, captured rate. There is no search box,
   no trend cards, no realization, and no draft/posted column.
5. Change **Rows per page** between 20, 50, and 100 and move to the next page.
   The footer count is the matching total, not the page length. The request
   must not attempt to load the rest of the 400k seed.
6. Select one row. The detail pane shows the same facts plus recorded / last
   revised times and the captured rate as read-only.

This is SC-001's walkthrough.

Compare the current page against
`GET http://localhost:5202/api/v1/time-entries?from=2026-08-10&to=2026-08-13&skip=0&take=20`
with the same token. The UI must not disagree with the JSON on duration,
billable flag, or captured rate.

## Validation 2 — empty is not an error

Keep the token. Set the range to `2030-01-07` .. `2030-02-04` and apply. The
listing must say there is no matching activity, not show a failed request and
not show the previous page as current.

Set `from` later than `to` and apply. The request must not go out. An
actionable message names the problem.

## Validation 3 — record, see the captured rate, then refuse a bad duration

Return to 2026-08-10 .. 2026-08-13.

**Accepting.** Record 6 minutes against an active timekeeper and an active
matter of an active client, work date **today**, a short narrative. The new
row appears. The captured rate is visible. There was no rate field to fill.

**Refusing (rule 1).** Record 7 minutes the same way. The page shows the
service's duration-increment sentence, does not claim success, and does not
add a row. The wording must match the problem document's `violations[].detail`,
not a locally invented "must be a multiple of 6".

Optional, same form: an inactive matter (labelled in the picker) produces the
active-matter refusal; a work date of tomorrow produces the backdating-window
refusal. Feature 005 already owns the exhaustive pairs; this walkthrough only
has to show that the UI surfaces them.

## Validation 4 — revise and delete without rewriting history

Open a seed entry whose work date is in **2024** (set the listing range back
to the start of the seed, e.g. `2024-08-13` .. `2024-08-20`).

- Change only the narrative. The save succeeds. The captured rate is unchanged
  and still not editable. The timekeeper cannot be changed.
- Change that same entry's work date to another 2024 date. The service refusal
  is shown; the stored date is unchanged.

Delete a **newly recorded** entry from validation 3: cancel the confirmation
and the row stays; confirm and it disappears from the listing.

## Validation 5 — the page is open, the API is not; Reports still works

In a private window, open `http://localhost:5202/#time-entries` **without**
pasting a token. The page loads. Applying filters without a token asks for
sign-in and does not display entries.

`GET http://localhost:5202/api/v1/time-entries?from=2026-08-10&to=2026-08-13`
without `Authorization` still returns 401.

From an authenticated session, choose **Reports**. The weekly rollup from 007
is still there. There is no control that registers a client, opens a matter, or
edits a timekeeper.

## Regenerating the UI (not part of the reviewer quickstart)

```powershell
cd web
npm ci
npm run build
```

`npm run build` replaces `src/LexTime.Api/wwwroot` from the fresh static export.
Then `dotnet run` serves the new files. A reviewer who does not change the UI
never runs this.
