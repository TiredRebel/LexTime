# Quickstart — Clients, Matters and Timekeepers (006)

How to prove the last ten endpoints work, and — more to the point — that closing a matter here
changes what the *other* features do.

**This feature adds no prerequisite and no step.** Ten more routes on a service the quickstart
already starts.

## Prerequisites

- Docker, running
- .NET SDK 9.0.x (`global.json` pins 9.0.317)

## Setup — still two commands

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

```powershell
dotnet run --project src/LexTime.Api
```

Swagger at `http://localhost:5202/swagger` now lists the full seventeen-endpoint surface of
`docs/prd.md` §4. The bootstrap prints the token the calls below need.

## Validation 1 — register a client, open a matter, bill against it

```powershell
$token = '<paste the token the bootstrap printed>'
$h = @{ Authorization = "Bearer $token"; 'Content-Type' = 'application/json' }
```

Register:

```powershell
$c = Invoke-RestMethod -Method Post -Uri 'http://localhost:5202/api/v1/clients' -Headers $h `
     -Body '{"clientCode":"QSTART","name":"Quickstart Holdings"}'
```

Open a matter under it:

```powershell
$m = Invoke-RestMethod -Method Post -Uri "http://localhost:5202/api/v1/clients/$($c.clientId)/matters" `
     -Headers $h -Body '{"matterNumber":"001","name":"First matter","isBillableByDefault":true}'
```

Record time against it, through feature 005's endpoint:

```powershell
$today = (Get-Date).ToString('yyyy-MM-dd')
Invoke-RestMethod -Method Post -Uri 'http://localhost:5202/api/v1/time-entries' -Headers $h `
  -Body "{`"userId`":1,`"matterId`":$($m.matterId),`"workDate`":`"$today`",`"durationMinutes`":60,`"isBillable`":true,`"narrative`":`"Quickstart.`"}"
```

**Expected**: `201` at each step, and the time entry comes back with an `hourlyRateSnapshot` that
was never sent.

**This chain is the whole point of user story 1.** A creation path that produced records the
write path then refused would have delivered nothing, so the third call is the one that proves
the first two.

## Validation 2 — the collisions

| Request | Expected |
| --- | --- |
| register `QSTART` again | `409`, naming `clientCode` |
| register `qstart` (lower case) | `409` — the codes are compared case-insensitively |
| open `001` under the **same** client again | `409`, naming `matterNumber` and **that client** |
| open `001` under a **different** client | `201` — numbers are unique within a client, not across the firm |
| open a matter under client `999999` | `404`, not `409` — a missing parent is a different mistake |
| register with `"clientCode": "  "` | `400` — malformed is checked before conflicting |

The third and fourth rows are the pair that matters. Feature 002's seed restarts matter numbering
at `001` for every client, so a global uniqueness reading would break the seeded dataset and
every report drawn from it. If row four returns `409`, the composite rule has been implemented as
a global one.

None of these should ever produce a `500`. A raw storage failure escaping is exactly what FR-008
forbids.

## Validation 3 — closing a matter, seen from the other side

This is the validation worth doing slowly. Close the matter:

```powershell
Invoke-RestMethod -Method Put -Uri "http://localhost:5202/api/v1/matters/$($m.matterId)" `
  -Headers $h -Body '{"name":"First matter","isBillableByDefault":true,"isActive":false}'
```

**Now try to record more time against it.** Expected: `400` from feature 005's rule 5, with the
refusal saying **the matter** is not active.

**Then reopen the matter and close the client instead**, and try again. Expected: `400` again, but
now saying **the client** is not active.

Those two messages are why deactivating a client does not cascade to its matters. If it did, a
matter under a closed client would itself be closed, and the second message could never be
produced — a branch feature 005 built and tested would become unreachable.

**Finally, confirm the closure erased nothing.** Request the weekly rollup for the week you
recorded in:

```powershell
Invoke-RestMethod -Uri "http://localhost:5202/api/v1/reports/weekly-billable-rollup?from=...&to=..." -Headers $h
```

**Expected**: the entry recorded in Validation 1 is still there, in the closed matter's client's
figures. Closing a matter stops new billing; it does not delete history. A plausible wrong
implementation would filter closed matters out of the report and would pass every test that only
looked at the write path.

## Validation 4 — timekeepers are read-only

```powershell
Invoke-RestMethod -Uri 'http://localhost:5202/api/v1/users?take=5' -Headers $h
```

**Expected**: five timekeepers with their current rates.

Then confirm what is *not* there:

```powershell
Invoke-WebRequest -Uri 'http://localhost:5202/api/v1/users' -Method Post -Headers $h `
  -Body '{}' -SkipHttpErrorCheck
```

**Expected**: not served — `405` or `404`, never `201`. There is no create route and no edit
route, and that absence is the enforcement. An assertion about something not existing is easy to
skip, and it is the only thing standing between "we decided not to" and "we forgot".

## Validation 5 — listing and paging

```powershell
Invoke-RestMethod -Uri 'http://localhost:5202/api/v1/clients?isActive=true&take=5' -Headers $h
Invoke-RestMethod -Uri 'http://localhost:5202/api/v1/clients?isActive=true&take=5&skip=5' -Headers $h
```

**Expected**: five each, no identifier in both, and a `total` larger than either page. Request
with no filter and confirm the result is still bounded by the default page size rather than
returning all sixty-one clients.

Also list one client's matters and confirm only that client's appear.

## Validation 6 — the gates

```powershell
dotnet build --warnaserror
```

```powershell
dotnet test
```

**Expected**: `0 Warning(s), 0 Error(s)`, and every earlier feature's tests still green —
feature 003's hand-computed rollup, feature 004's index equivalence, and feature 005's rule tests
all unchanged. **If a rule test had to be edited to accommodate this feature, that is a defect
here**, not an update: nothing in this feature is allowed to change what the rules do.

## What this feature deliberately does not do

- **No delete for clients or matters.** `docs/prd.md` §2.2 has no such endpoint and time entries
  reference both. Deactivation is the domain's answer.
- **No change to a code or a matter number after creation.** They are how the firm refers to
  these records outside this system.
- **No creating or editing timekeepers.** Seeded and read-only; an editable rate would need the
  rate history §2.2 rules out.
- **No change to the rules, the rollup, its procedure or its index.**
