# Quickstart — Weekly Billable Rollup (003)

How to prove this feature works end to end, from cold. Every step below is runnable by a
reviewer who has only Docker and the .NET 9 SDK — **this feature adds no prerequisite**, which
is the P18 claim it has to keep.

## Prerequisites

- Docker, running
- .NET SDK 9.0.x (`global.json` pins 9.0.317 with `rollForward: latestFeature`)

Not needed: `dotnet-ef`, `sqlcmd` on the host, a licence key, an account. `sqlcmd` is used
below only for the optional direct-procedure check, and it is invoked *inside* the container.

## Setup — still two commands

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

```powershell
dotnet run --project src/LexTime.Api
```

The first command now has one more thing to do than it did in feature 002: its
`apply-procedures` step, which previously reported `no procedures to apply`, finds
`usp_WeeklyBillableRollup.sql` and applies it. Expect that line to change to `1 applied`.

**That change is the P18 check for this feature.** If the quickstart needed a third command,
or a manual step to install the procedure, the feature would be defective regardless of
whether the report is correct.

The bootstrap prints a development token at the end of its run. Copy it — the calls below need
it.

## Validation 1 — the report, called directly

The strongest check, and the one SC-009 exists for: the procedure returns every figure already
computed, with no application code involved at all.

```powershell
docker exec lextime-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'LexTime!Dev2026' -C -d LexTime -W -Q "EXEC dbo.usp_WeeklyBillableRollup @FromDate='2026-01-05', @ToDate='2026-02-01';"
```

**Expected**: rows carrying all twelve columns, including `CumulativeBillableHours`,
`HoursDeltaVsPriorWeek` and `ClientRankInWeek`. Chronological, busiest client first within each
week.

**What to look at:**

- `HoursDeltaVsPriorWeek` is `NULL` on every row of the **first** reported week and populated
  afterwards. That is the range boundary, not a bug — the week before the range is not visible.
- `ClientRankInWeek` runs `1, 2, 3…` within a week and does not restart mid-week.
- `CumulativeBillableHours` equals `BillableHours` on a client's first row and never decreases.
- Billable hours are heavily uneven across clients. That is feature 002's seed shape (P9)
  showing through; a flat distribution would mean the seed regressed, not the report.

Then confirm the filter does not distort the row it returns:

```powershell
docker exec lextime-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'LexTime!Dev2026' -C -d LexTime -W -Q "EXEC dbo.usp_WeeklyBillableRollup @FromDate='2026-01-05', @ToDate='2026-02-01', @ClientId=1;"
```

**Expected**: only that client's weeks, with `ClientRankInWeek` still showing its true standing
among all clients. A column of `1`s would mean the rank was computed after the filter — FR-012
violated.

## Validation 2 — the endpoint

```powershell
$token = '<paste the token the bootstrap printed>'
curl -H "Authorization: Bearer $token" "http://localhost:5000/api/v1/reports/weekly-billable-rollup?from=2026-01-05&to=2026-02-01"
```

**Expected**: `200` and a JSON envelope echoing `from` and `to` with a `rows` array whose
figures match Validation 1 exactly. They come from the same procedure; if they differ, the
reader is transforming something it should not be.

Then the refusals:

| Request | Expected |
| --- | --- |
| no `Authorization` header | `401`, no report data in the body |
| `from=2026-03-29&to=2026-01-05` | `400` problem+json naming both dates |
| `from` omitted | `400` — no default range is assumed |
| a range with no seeded activity, e.g. `from=2030-01-07&to=2030-02-04` | `200` with `"rows": []` |
| `clientId=999999` | `200` with `"rows": []`, **not** `404` |

Swagger at `http://localhost:5000/swagger` lists the endpoint and is reachable without a
token, as it was before.

## Validation 3 — the tests

```powershell
dotnet test
```

**Expected**: green, with the feature-003 additions on top of the 40 tests features 001 and 002
left passing.

The tests that matter most here are the procedure-level ones. They call
`dbo.usp_WeeklyBillableRollup` directly against a small fixture whose expected running totals,
week-on-week changes and ranks were computed by hand and written into the test before the
procedure was run (P12, FR-021). Three of them are worth knowing by name:

- **the gap case** — a client that bills, goes quiet for several weeks, then bills again. Its
  returning week's change must be that week's own hours, *not* the difference against the week
  it last billed in. The fixture is built so those two numbers differ, or the test would pass
  under either implementation (R7).
- **the year-boundary gap** — the same shape, spanning New Year. ISO week 1 follows week 52 or
  53 depending on the year, so anything deriving "last week" from the week number is wrong every
  January and right the rest of the time (FR-023, R1).
- **the empty range** — window functions over no rows. Reporting calculations that accumulate
  across rows commonly fail here in a way no populated test detects (FR-024).

## Validation 4 — the build gate

```powershell
dotnet build --warnaserror
```

**Expected**: `0 Warning(s), 0 Error(s)`. This feature adds no analyzer suppression. Under R5,
a `CA2100` suppression appearing anywhere in it is a design error rather than something to
justify — the procedure takes three scalar parameters and its command text does not vary.

## What this feature deliberately does not show

**No performance numbers.** Not in the README, not here, not in a code comment. The covering
index and the before-and-after measurement are the next feature's deliverable, and this one
ships against the day-one index set on purpose so that measurement has an honest "before"
(P8, PRD §3).

If the report feels slow over the full 24-month range, that is the expected state and the
point. Do not add an index to make it feel better before the measurement has been taken.
