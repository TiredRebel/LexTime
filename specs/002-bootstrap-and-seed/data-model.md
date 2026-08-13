# Phase 1 Data Model: Bootstrap and Seed

**Feature**: 002-bootstrap-and-seed | **Date**: 2026-08-13

This feature adds **no tables, no columns and no migrations**. The schema is feature 001's
and is documented in
[its data-model](../001-solution-and-schema/data-model.md). What follows is the shape of
the data this feature generates into that schema, and the one new configuration type it
introduces.

---

## SeedOptions

The generator's entire input. Everything that varies between a production seed and a test
seed lives here, which is what lets the tests run the real generator at 1/100 scale
(P13, plan Constitution Check).

| Property | Default | Notes |
|---|---|---|
| `UserCount` | 25 | |
| `ClientCount` | 60 | |
| `MatterCount` | 220 | Distributed unevenly across clients |
| `TimeEntryCount` | 400,000 | The only value tests reduce meaningfully |
| `MonthsOfHistory` | 24 | |
| `ReferenceDate` | **2026-08-13** | The newest date any entry may carry |
| `RandomSeed` | fixed integer constant | Drives the single generator instance |
| `InactiveShare` | 0.12 | Fraction of users, clients and matters marked inactive |
| `NonBillableShare` | 0.18 | Fraction of entries marked non-billable |
| `WeekendShare` | 0.05 | Fraction of entries falling on Saturday or Sunday |

**`ReferenceDate` is not free to change.** Feature 001 shipped
`WorkDateConstraintTests.AcceptsWorkDateAtTheOldestSeededBoundary`, asserting that
`2024-08-13` is accepted as the far edge of what this feature seeds. A 24-month window back
from `2026-08-13` lands exactly there. Moving the anchor without moving that test leaves a
test that passes and no longer means anything — see research.md R2.

**Nothing in the generation path may read ambient state.** No `DateTime.Now`, no
`DateTime.UtcNow`, no `Random.Shared`, no `Guid.NewGuid`. Every value derives from
`SeedOptions` (FR-020, FR-021).

---

## Generated shape

### Users — 25

| Aspect | Rule |
|---|---|
| `Email` | Deterministic, unique, obviously synthetic (a `@lextime.test` domain) |
| `DefaultHourlyRate` | Spread across a plausible range rather than uniform — partners and juniors do not bill alike |
| `IsActive` | ~12% inactive (FR-016) |
| `CreatedAtUtc` | Derived from `ReferenceDate`, not from the clock |

### Clients — 60

| Aspect | Rule |
|---|---|
| `ClientCode` | Deterministic, unique, uppercase |
| `IsActive` | ~12% inactive, **including at least one with billable history** (SC-007) |

### Matters — ~220

| Aspect | Rule |
|---|---|
| Distribution | Uneven across clients: a few large clients carry many matters, a long tail carries one or two (FR-014) |
| `MatterNumber` | Unique **within** its client. Numbers repeat across clients deliberately — that is the case feature 001's composite index exists for, and generating globally unique numbers would leave it unexercised at volume |
| `IsBillableByDefault` | Mostly true; a minority pro bono |
| `IsActive` | ~12% inactive, including some with history |

### TimeEntries — ~400,000

| Aspect | Rule |
|---|---|
| `WorkDate` | Uniform across 24 months **except** for weekday concentration — weekend entries under 10% (FR-013, SC-004) |
| Client skew | The ten busiest clients account for at least half of all logged minutes (SC-004). Follows from matter distribution plus per-client intensity |
| `DurationMinutes` | Always a positive multiple of 6, never above 1440. Clustered at realistic values — 6, 12, 30, 60, 90 are common; 1440 essentially never occurs |
| Daily total | No user exceeds 1440 minutes on a date. Not a schema constraint (rule 3 is application-layer, feature 004), but generating data that violates it would make the seed unreproducible through the API it claims to model |
| `IsBillable` | ~18% non-billable (FR-015, SC-004) |
| `HourlyRateSnapshot` | Copied from the user's rate at generation. Not recomputed, not a foreign key |
| `CreatedAtUtc` | Derived from `WorkDate` plus a deterministic offset — entries are typed up near when the work happened, not all at one instant |
| `UpdatedAtUtc` | Null for the overwhelming majority |

**Ageing is expected, not a defect.** Most entries are older than 90 days. The backdating
limit governs submissions through the API, not recorded history (FR-019), which is why
feature 001 deliberately left `WorkDate` unconstrained.

---

## Load path

`SqlBulkCopy`, not EF change tracking. Inserting 400,000 tracked entities takes minutes and
would miss FR-022's sub-minute target by an order of magnitude.

Consequences worth naming rather than discovering:

- **Application validation is bypassed entirely.** The database's `CHECK` and unique
  constraints are the only thing between a generator bug and a corrupt dataset. This is the
  scenario feature 001's User Story 2 was written for.
- **Column ordering in the bulk mapping is explicit**, never positional. A positional
  mapping silently loads `UserId` into `MatterId` the first time a column is added.
- **The load is one transaction per table**, so a failure leaves the database empty rather
  than partially loaded — which is what makes R6's "partial" state rare enough to be an
  error path rather than a routine one.
- **Identity columns are not supplied.** `UserId`, `ClientId`, `MatterId` and `TimeEntryId`
  are database-assigned, so the generator works in terms of its own indices and the loader
  resolves real keys after each parent table lands.

---

## Verification queries

Run by the script after loading, at full volume (FR-023, research.md R7). Each maps to a
success criterion and each has a declared band:

| Check | Band | Criterion |
|---|---|---|
| Weekend share of entries | < 10% | SC-004 |
| Non-billable share | 10–25% | SC-004 |
| Top ten clients' share of logged minutes | ≥ 50% | SC-004 |
| Duration rule violations | exactly 0 | SC-005 |
| Entries dated after the reference date | exactly 0 | SC-005 |
| Inactive share, per entity | 10–15% | SC-007 |
| Inactive clients, matters and users **with** history | ≥ 1 each | SC-007 |
| Row counts per table | match `SeedOptions` | R6 |

A band miss is a non-zero exit, not a warning. A seed that quietly falls outside its own
stated shape is worse than one that fails, because feature 003 will report on it as though
it were sound.
