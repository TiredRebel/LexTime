# Phase 1 Data Model: Solution and Schema

**Feature**: 001-solution-and-schema | **Date**: 2026-08-12

Four tables in schema `dbo`. Column types are fixed by `docs/prd.md` §3 and are repeated
here only where this feature adds a constraint or a rule the PRD leaves implicit.

---

## Users

Timekeepers. Seeded and read-only through the API for the life of the project
(`docs/prd.md` §2.2 — no registration, no user management).

| Column | Type | Rules |
|---|---|---|
| `UserId` | `int IDENTITY` | PK |
| `Email` | `nvarchar(256)` | Unique index. Required |
| `FullName` | `nvarchar(200)` | Required |
| `DefaultHourlyRate` | `decimal(10,2)` | USD. Copied onto entries at creation, never read back through them |
| `IsActive` | `bit` | Forward-looking only — see Lifecycle below |
| `CreatedAtUtc` | `datetime2(3)` | Required |

---

## Clients

| Column | Type | Rules |
|---|---|---|
| `ClientId` | `int IDENTITY` | PK |
| `ClientCode` | `nvarchar(20)` | Unique index. Required |
| `Name` | `nvarchar(200)` | Required |
| `IsActive` | `bit` | Forward-looking only |
| `CreatedAtUtc` | `datetime2(3)` | Required |

---

## Matters

A matter belongs to exactly one client.

| Column | Type | Rules |
|---|---|---|
| `MatterId` | `int IDENTITY` | PK |
| `ClientId` | `int` | FK → `Clients.ClientId`, indexed. Required |
| `MatterNumber` | `nvarchar(30)` | Unique **within** its client — composite unique index on (`ClientId`, `MatterNumber`), not a global unique index |
| `Name` | `nvarchar(250)` | Required |
| `IsBillableByDefault` | `bit` | Default for entries; an entry may override it |
| `IsActive` | `bit` | Forward-looking only |
| `CreatedAtUtc` | `datetime2(3)` | Required |

The composite index is the detail most easily got wrong: two different clients may each
have a matter numbered `001`, and a global unique index would reject the second one.

---

## TimeEntries

| Column | Type | Rules |
|---|---|---|
| `TimeEntryId` | `bigint IDENTITY` | PK, clustered |
| `UserId` | `int` | FK → `Users.UserId`. Required |
| `MatterId` | `int` | FK → `Matters.MatterId`. Required |
| `WorkDate` | `date` | The billing date, not the date of entry. No check constraint — see Rules below |
| `DurationMinutes` | `int` | `CHECK (DurationMinutes > 0 AND DurationMinutes % 6 = 0 AND DurationMinutes <= 1440)` |
| `IsBillable` | `bit` | Required |
| `HourlyRateSnapshot` | `decimal(10,2)` | Copied from the user at creation. Not a foreign key, not recomputed |
| `Narrative` | `nvarchar(1000)` | |
| `CreatedAtUtc` | `datetime2(3)` | Required |
| `UpdatedAtUtc` | `datetime2(3)` | Nullable |

### Indexes in this feature

Primary keys and foreign key indexes only. The covering index
`IX_TimeEntries_WorkDate_Billable` is **deliberately absent** (FR-013) so that feature 002
has a genuine unindexed baseline to measure against. Adding it here would destroy the
comparison constitution P8 requires.

---

## Rules: where each one is enforced

The six domain rules in `docs/prd.md` §2.1 do not all belong to the same layer, and this
feature implements only the storage half (constitution P6, FR-011).

| Rule | Storage | Application | In this feature |
|---|---|---|---|
| 1. Duration is a positive multiple of 6 | `CHECK` constraint | Validation with a clear message | Storage only |
| 2. Entry ≤ 1440 minutes | `CHECK` constraint | Validation with a clear message | Storage only |
| 3. Per user per day ≤ 1440 minutes | Not expressible as a `CHECK` | Query at creation time | Neither — feature 003 |
| 4. `WorkDate` not future, not >90 days past | **No constraint** — see below | Validation at creation time | Neither — feature 003 |
| 5. Matter and client must be active | Not expressible as a `CHECK` | Query at creation time | Neither — feature 003 |
| 6. Rate snapshotted at creation | Column exists | Populated at creation | Column only |

### Rule 4 is deliberately not a constraint

Rule 4 governs what may be **submitted**, not what may **exist**. A `CHECK` on `WorkDate`
would reject the seeded history the moment it was written, and would make the database
progressively reject its own contents as time passed. This was a contradiction in the
first draft of the spec, resolved in clarification Q1 and recorded as FR-018a. `docs/prd.md`
§3 agrees — it specifies a `CHECK` on `DurationMinutes` and none on `WorkDate`.

The only date rule this feature enforces is that no seeded entry may be dated after the
seed's reference date. That is a property of the generator, not a schema constraint.

---

## Lifecycle: what `IsActive` means

`IsActive` is forward-looking on all three entities that carry it (FR-020b). Setting it
false prevents new time entries being recorded against that client, matter or timekeeper.
It does **not** remove, hide or invalidate entries recorded while it was true.

There are no other state transitions. There is no approval, submission or lock workflow
anywhere in this project (`docs/prd.md` §2.2), and no soft delete — deletes are hard
deletes and only `CreatedAtUtc`/`UpdatedAtUtc` are tracked.

**This has a consequence feature 002 must resolve, not inherit silently**: the seed
guarantees that at least one inactive client has billable history (SC-010), so the weekly
rollup will encounter a closed client with real activity in a reported period. Whether it
includes them is a decision that feature's spec must state.

---

## Seeded volumes and shape

Not this feature. The schema ships empty; volumes, distribution bands and the fixed
reference date are specified in
[feature 002](../002-bootstrap-and-seed/spec.md) under FR-012 to FR-021.

What this feature owes that feature is the constraint surface those 400,000 rows will be
written through. The bulk load path bypasses application validation entirely, so the
`CHECK` and unique constraints above are the only thing standing between a generator bug
and a corrupt dataset.
