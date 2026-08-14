# Quickstart — Time Entries and the Domain Rules (005)

How to prove the six rules are enforced, reached from every path, and not merely present.

**This feature adds no prerequisite and no step.** It adds five endpoints to a service the
quickstart already starts.

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

The bootstrap prints a development token. The calls below need it, and Swagger at
`http://localhost:5202/swagger` will list the five new routes.

## Validation 1 — the rules, without a database

```powershell
dotnet test --filter "FullyQualifiedName~TimeEntryRuleTests"
```

**Expected**: green, in well under a second. No container starts.

This is the tier that matters most and the one to read first. Every rule has a refusing test and
an accepting test, and every boundary is tested at the limit and one step past it. The rules are
a pure function of a facts record, so exhaustiveness is cheap here — which is the point. A rule
suite that needed a database per case would be slow enough that nobody would grow it.

**Two boundaries worth knowing about:**

- **Rule 2's boundary is `1446`, not `1441`.** `1441` proves nothing about the 24-hour maximum,
  because rule 1 refuses it first for not being a multiple of six. The first value that isolates
  rule 2 is the next legal increment above the limit.
- **Rule 3's interesting case is a *reduction*.** An entry of 600 minutes on a day already
  totalling 1,440 must be reducible to 300. An implementation that counts the entry against
  itself refuses that, and no test of an *increase* would notice.

## Validation 2 — the rules are actually reached

```powershell
dotnet test --filter "FullyQualifiedName~TimeEntryWriteTests"
```

**Expected**: green, against a real container.

The two tiers ask different questions and both are needed. The pure tests ask *is the rule
right*; these ask *is the rule reached*. A feature that enforced all six perfectly in a class
nothing called would pass Validation 1 completely.

These also cover the two things a pure test cannot see: that **a refused write leaves the stored
row byte-identical**, and that the **storage constraint still refuses a violating row written
outside the application**.

## Validation 3 — by hand, against the seeded data

```powershell
$token = '<paste the token the bootstrap printed>'
$h = @{ Authorization = "Bearer $token"; 'Content-Type' = 'application/json' }
$base = 'http://localhost:5202/api/v1/time-entries'
```

**Record something valid** — use a work date within the last 90 days, and an active matter:

```powershell
$body = '{"userId":1,"matterId":1,"workDate":"REPLACE","durationMinutes":90,"isBillable":true,"narrative":"Quickstart check."}'
Invoke-RestMethod -Method Post -Uri $base -Headers $h -Body ($body -replace 'REPLACE', (Get-Date).ToString('yyyy-MM-dd'))
```

**Expected**: `201`, with a `hourlyRateSnapshot` you did not send. That field being populated
from the timekeeper rather than the request is rule 6 working.

**Then break each rule and read what comes back.** Every refusal should name the rule and the
offending value:

| Change | Expected |
| --- | --- |
| `durationMinutes: 7` | `400`, `DurationIncrement`, naming `7` |
| `durationMinutes: 1446` | `400`, `DurationMaximum`, naming the 1,440 limit |
| `workDate` set to tomorrow | `400`, `BackdatingWindow` |
| `workDate` set to 200 days ago | `400`, `BackdatingWindow`, naming the 90-day limit |
| `matterId` of an inactive matter | `400`, `ActiveMatterAndClient`, **saying which of the two was inactive** |
| `durationMinutes: 7` and a future `workDate` together | `400` listing **both** violations |

That last row is the one to check deliberately. A submission wrong in two ways should not take
two round trips to fix.

**Then correct an old entry.** Find a seeded entry older than 90 days, `PUT` it back with only
its narrative changed:

**Expected**: `200`. Rule 4 does not fire, because the work date is not being changed. Now `PUT`
it again with the work date moved by one day:

**Expected**: `400`, `BackdatingWindow`. That pair is the clarification made observable — an old
entry can be corrected but not re-dated.

**And confirm the rate is not rewritten.** Note a recorded entry's `hourlyRateSnapshot`, then
`PUT` it with a different narrative and read it back. The rate must be unchanged. An update
handler that rebuilt the entity and re-read the current rate would silently rewrite history on
every edit, and only this check would notice.

## Validation 4 — listing and paging

```powershell
Invoke-RestMethod -Uri "$base`?userId=1&take=5" -Headers $h
Invoke-RestMethod -Uri "$base`?userId=1&take=5&skip=5" -Headers $h
```

**Expected**: five entries each, no identifier appearing in both, and a `total` far larger than
either page.

Also request with **no filters at all**: the result must still be bounded by the default page
size rather than returning 400,000 rows.

## Validation 5 — the report still sees what the write path writes

Record an entry dated inside a week the rollup covers, then request that week:

```powershell
Invoke-RestMethod -Uri "http://localhost:5202/api/v1/reports/weekly-billable-rollup?from=...&to=..." -Headers $h
```

**Expected**: the new entry's minutes are included in that week's figures for its client.

The rollup is not modified by this feature, but an entry the write path accepts and the report
cannot see would be a defect in this feature — the two halves have to agree about what a time
entry is.

## Validation 6 — the gates

```powershell
dotnet build --warnaserror
```

```powershell
dotnet test
```

**Expected**: `0 Warning(s), 0 Error(s)`, and every earlier feature's tests still green —
including feature 003's rollup expectations and feature 004's covering-index equivalence, both
unchanged.

## What this feature deliberately does not do

- **No CRUD for clients, matters or timekeepers.** Feature 006. The seed supplies all three.
- **No change to the rollup, its procedure or its index.** Feature 004 measured that; a write-path
  change in the same feature would put the measurement in question.
- **No approval, submission or locking workflow**, and deletion is not gated by any period rule.
  `docs/prd.md` §2.2.
- **No ownership model.** Any caller may record time for any timekeeper; the token proves trust,
  not identity.
