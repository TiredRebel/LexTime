# Quickstart — Index and Measured Performance (004)

How to prove this feature works, and — more to the point — how a reviewer regenerates its
numbers instead of believing them.

**This feature adds no prerequisite.** That is the claim it has to keep, and it is why the
measurement is a verb on the application rather than a script that needs a database tool.

## Prerequisites

- Docker, running
- .NET SDK 9.0.x (`global.json` pins 9.0.317 with `rollForward: latestFeature`)

Not needed: `sqlcmd` on the host, SSMS, Azure Data Studio, `dotnet-ef`, any benchmarking tool,
any account.

To *open* the committed `.sqlplan` files you will want SSMS or Azure Data Studio — but you do
not need either to reproduce the numbers, only to look at the plans.

## Setup — still two commands

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

```powershell
dotnet run --project src/LexTime.Api
```

Nothing about this changes. The migration step now applies one more migration; the reviewer
sees a different count and no different instruction.

## Validation 1 — the index is there and the schema knows it

```powershell
dotnet run --project src/LexTime.Api state
```

**Expected**: the state report now names the covering index and confirms it is present.

That line exists because of the trap in R7: once the index is created by a migration, EF records
the migration as applied and re-running `migrate` will **not** restore the index if something
dropped it. A database in that condition reports itself fully migrated and is quietly missing an
index. `state` is where a developer asks what condition the database is in, so it is where the
question gets answered.

## Validation 2 — the report is unchanged by the index

```powershell
dotnet test --filter "FullyQualifiedName~CoveringIndexTests"
```

**Expected**: green. The test runs the rollup, drops the index, runs it again, and compares
every field of every row before restoring.

This is the first thing to check and the reason it is user story 1 rather than a footnote. An
index that changes results is the failure that hides best: every figure stays plausible, stays
self-consistent, and nobody looks.

The same claim at full scale is discharged by Validation 3, which hashes both result sets over
all 400,000 entries.

## Validation 3 — regenerate the measurement

```powershell
dotnet run --project src/LexTime.Api measure
```

**Before you run it**: this clears the SQL Server buffer pool between readings, for the whole
instance rather than one database. Harmless against the local container the quickstart brings
up. Do not point it at anything shared.

**Expected**: a table of four combinations — full range and single client, each with and without
the index — carrying logical reads, a median elapsed time with its range, the row count, and an
equivalence verdict. Then the paths it wrote.

**What must match the published figures exactly:**

- **logical read counts**, for all four combinations. They are a property of the plan and do not
  depend on your hardware. If yours differ, something differs about the data or the schema, and
  that is worth knowing.
- **the result hashes**, and the equivalence verdict.
- **the row counts**.

**What will legitimately differ:**

- **elapsed times**, on every axis — median, minimum, maximum. Different CPU, different disk,
  different container memory, different neighbours. This is why the published account leads with
  read counts and treats elapsed time as illustration.

Run it twice and compare: the read counts and hashes will be identical between your two runs,
and the elapsed times will not be. That contrast is itself the argument for how the numbers are
weighted.

**After it finishes**, the database is indexed again — including if it failed partway. Check
with `state` if you want to confirm rather than trust.

## Validation 4 — the published account matches its evidence

Open [docs/performance.md](../../docs/performance.md) and pick any figure from its summary
table. Find it in the corresponding `docs/performance/statistics-*.txt`, which is the verbatim
output the server produced.

That is the point of committing the raw text: the table is a transcription, and a transcription
is somewhere a number can quietly change. Nothing in the summary should be unfindable in the
raw files.

Then open the two `.sqlplan` files for one shape side by side and read the plan-shape paragraph
against them. What the paragraph names should be visible in the plans — a sort that is gone, a
scan that became a seek, a spill that stopped. **If the paragraph says the plan shape did not
change, that is a permitted answer** and the plans should show that too.

## Validation 5 — the build gate

```powershell
dotnet build --warnaserror
```

**Expected**: `0 Warning(s), 0 Error(s)`.

```powershell
dotnet test
```

**Expected**: green, with the feature-003 tests passing **unchanged**. If an expected value in
`WeeklyBillableRollupTests` had to be edited to accommodate the index, that is a defect in this
feature — the index is not allowed to change what the report returns, and a test that was
adjusted to agree with it has been made useless.

## What this feature deliberately does not do

- **It does not rewrite the procedure.** The measurement is of the index. A query change in the
  same feature makes it impossible to say which caused what.
- **It does not add a second index**, tune statistics, or add query hints. One index, one story,
  before and after.
- **It does not enlarge the seed to improve the result.** If the index helps less than hoped,
  the modest number is what gets published, with an explanation of why it is modest. The
  alternative would invalidate feature 002's committed dataset and every test asserting its
  volumes, to make one figure look better.
