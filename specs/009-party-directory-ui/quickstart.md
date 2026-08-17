# Quickstart — Party Directory UI (009)

How to prove clients, matters, and timekeepers are operable in a browser from
the same two commands that already start the API.

**Node is not required for this walkthrough.** It is required only when changing
the UI source under `web/` and regenerating `wwwroot`.

Field meanings are in [data-model.md](./data-model.md). The JSON the UI must
not disagree with is in
[feature 006's contracts](../006-clients-and-matters/contracts/client-endpoints.md).

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

## Validation 1 — find a client and its matters

1. Paste the token into the sign-in field.
2. Choose **Clients** in the sidebar (or open
   `http://localhost:5202/#clients`).
3. The status filter should already show **All**. There is no search box and
   no Active/Inactive/Total count cards.
4. Confirm a bounded table: code, name, active flag, registration time.
   Change **Rows per page** between 20, 50, and 100 and move to the next page.
   The footer count is the matching total, not the page length.
5. Restrict to **Inactive**. Confirm the matching total shrinks and every
   visible row is inactive. Return to **All**.
6. Select a known seeded client. The detail pane shows the same facts. The
   nested matter table lists only that client's matters (number, name,
   default billable, active). There is no Matters item in the sidebar.

This is SC-001's walkthrough.

Compare the current client page against
`GET http://localhost:5202/api/v1/clients?skip=0&take=20`
with the same token. The UI must not disagree with the JSON on code, name, or
active flag.

## Validation 2 — empty is not an error; missing is not empty

Keep the token. If a seeded client has no matters, its matter table must say
there are none, not show a failed request and not show another client's
matters.

A client listing restricted so that nothing matches (if the walkthrough can
produce that) must be an explicit empty success, not the previous page.

There is no **Matters** destination that lists the firm.

## Validation 3 — register, collide, then open a matter

**Accepting.** Register a client with an unused code (for example `WALK`) and
a name. The new row appears, active. There was no inactive-at-create control.
The code is visible and not offered as an editable field on correct.

**Refusing (taken code).** Register again with `WALK`. The page shows the
service conflict naming `clientCode` and `WALK`, does not claim success, and
does not add a row.

**Refusing (case).** Register with `walk`. The same kind of conflict appears.
The UI must not treat this as a malformed field.

**Accepting matter.** Under the new client, open matter `001` with a name and
default billable yes. It appears only under that client.

**Refusing (taken number).** Open `001` again under **that same client**. The
conflict names `matterNumber` and `001`. Open `001` under a **different**
seeded client. It succeeds.

**Missing parent.** This path is hard to drive from a selected-client UI by
construction; the walkthrough does not invent a free-typed client id. Feature
006 already owns that pair. If a 404 occurs on open, it must look like a
missing client, not a uniqueness conflict.

This is SC-002 and SC-003.

## Validation 4 — correct, close, and leave history alone

On the new client: change only the name. The save succeeds. The code is
unchanged. Deactivate it. Nested matters keep their own flags. Choose
**Reports**, set a range that includes seed history for a **seeded** client
you then deactivate (pick one with rollup rows in 2026-06-18 .. 2026-08-13),
note the figures, deactivate, reload the same range: the figures must not
drop to empty because of the closure.

On a new matter: change the name and default billable flag; deactivate;
reactivate. The number and owning client never appear as inputs.

Choose **Timekeepers**. Confirm a paged roster with name, email, current rate,
and active flag. Open one. The pane is labelled read-only. There is no Add,
no rate field, and no active toggle.

There is no delete control on clients or matters.

This is SC-005 and SC-010.

## Validation 5 — the page is open, the API is not; other views still work

In a private window, open `http://localhost:5202/#clients` **without** pasting
a token. The page loads. Loading the listing without a token asks for sign-in
and does not display clients.

`GET http://localhost:5202/api/v1/clients` without `Authorization` still
returns 401. `GET http://localhost:5202/api/v1/users` without `Authorization`
still returns 401.

From an authenticated session, choose **Time entries** and **Reports**. Both
still work. Time entries still has no register-client control of its own.

## Regenerating the UI (not part of the reviewer quickstart)

```powershell
cd web
npm ci
npm run build
```

`npm run build` replaces `src/LexTime.Api/wwwroot` from the fresh static export.
Then `dotnet run` serves the new files. A reviewer who does not change the UI
never runs this.
