# Data Model — Time Entries and the Domain Rules (005)

**No schema change. No migration.** The tables, columns, constraints and indexes are exactly as
features 001 and 004 left them. What this feature adds is a write path and the rules that guard
it, and everything below is either an in-memory type or an existing entity gaining behaviour.

A migration appearing in this feature would mean something has gone wrong.

## Domain types

Four new types in `LexTime.Domain/Rules/`. All immutable, all free of persistence, HTTP or any
notion of where their inputs came from.

### `DomainRule`

The six of `docs/prd.md` §2.1, named and enumerated so a violation can say which one it is and a
client can branch on it without parsing English.

| Value | Rule |
| --- | --- |
| `DurationIncrement` | 1 — a positive whole number of minutes, a multiple of six |
| `DurationMaximum` | 2 — a single entry may not exceed 24 hours |
| `DailyMaximum` | 3 — one timekeeper's total for one date may not exceed 24 hours |
| `BackdatingWindow` | 4 — not in the future, not more than 90 days past |
| `ActiveMatterAndClient` | 5 — an active matter belonging to an active client |
| `RateSnapshot` | 6 — the rate is captured at recording and never rewritten |

Rule 6 is in the enumeration for completeness even though it cannot be *violated* by a
submission — it is a rule about what the system does, not about what the caller sends. It is
enforced by the update command having no rate field at all (research R8), and its accepting test
is the one that proves a later rate change leaves history alone.

A seventh check — no entry against an inactive timekeeper (FR-013) — is `ActiveTimekeeper`. It is
not one of the six the PRD lists and is flagged as an addition in the spec's checklist.

### `TimeEntryFacts`

Everything the rules must be told, because four of the six cannot be evaluated from a submission
alone.

| Field | Type | Which rule needs it |
| --- | --- | --- |
| `DurationMinutes` | `int` | 1, 2, 3 |
| `WorkDate` | `DateOnly` | 4 |
| `Today` | `DateOnly` | 4 — supplied, never read from a clock inside the domain |
| `OtherMinutesOnDate` | `int` | 3 — the timekeeper's total for that date **excluding the entry being changed** |
| `MatterIsActive` | `bool` | 5 |
| `ClientIsActive` | `bool` | 5 — separate from the matter's, so the refusal can say which failed |
| `TimekeeperIsActive` | `bool` | FR-013 |
| `EvaluateWorkDate` | `bool` | 4 — false when an update leaves the work date untouched |
| `EvaluateMatter` | `bool` | 5 — false when an update leaves the matter untouched |

The last two are what the clarification decided: on update, rules 4 and 5 apply only to fields
actually being changed. They are flags on the facts rather than branches in the caller, so the
decision lives with the rules instead of being re-implemented in two handlers.

`OtherMinutesOnDate` carries the word *other* deliberately. On create it is the day's whole
total; on update it excludes the entry being revised, because counting an entry against itself
would refuse a duration reduction.

### `RuleViolation`

| Field | Notes |
| --- | --- |
| `Rule` | Which `DomainRule` failed |
| `OffendingValue` | The value that failed it, rendered for display |
| `Detail` | One sentence naming the value and the limit |

Returned, never thrown (research R6). A refused submission is an ordinary outcome of a
well-formed request.

### `TimeEntryRuleSet`

One evaluation: facts in, violations out. No I/O, no clock, no state. This is the single place
FR-011 requires, and the only place any of the rules is expressed.

## Application types

In `LexTime.Application/TimeEntries/`.

| Type | Purpose |
| --- | --- |
| `RecordTimeEntryCommand` | timekeeper, matter, work date, duration, billable flag, narrative. **No rate** — it is captured, not supplied |
| `ReviseTimeEntryCommand` | matter, work date, duration, billable flag, narrative. **No rate and no timekeeper** — neither is revisable |
| `ListTimeEntriesQuery` | optional timekeeper, matter, from and to; plus skip and take |
| `TimeEntryDto` | what the API returns, with its `ToDto()` extension beside it (P4) |
| `ITimeEntryStore` | the port; see plan.md's P4 note and research R2 |

`ReviseTimeEntryCommand` omitting the timekeeper is a decision, not an oversight: moving an entry
between timekeepers would change whose daily total it counts against and whose rate it should
have captured, and neither has a defined answer. Re-record it instead.

## Existing entities, and what changes

### `TimeEntry` — gains a write path, no new columns

Every field already exists. `CreatedAtUtc` is set on record; `UpdatedAtUtc` is set on revise and
stays null until then — which is what makes it meaningful.

`HourlyRateSnapshot` is the field the whole of rule 6 is about. Written once, never recomputed,
and absent from the revise command so the mistake cannot be made through the API.

### `User`, `Matter`, `Client` — read-only here

`User` supplies the current rate and its active flag. `Matter` supplies its active flag and its
client. `Client` supplies its own. All three are made writable by feature 006; this feature only
asks them questions.

### The duration `CHECK` constraint — unchanged, and re-proved

`CK_TimeEntries_DurationMinutes` stays exactly as feature 001 wrote it. Rules 1 and 2 now also
run in C#, and P6 calls that duplication deliberate. SC-010 writes a violating row outside the
application and asserts the database still refuses it — so deleting the constraint on the grounds
that "the application checks it now" fails a test rather than passing review.

### `WorkDate` — still carries no constraint

Rule 4 governs what may be *submitted*, not what may exist. Seeded history spans 24 months and
most of it is far outside the 90-day window; that is correct and settled in feature 002. A
constraint here would reject the seed and would make the database progressively reject its own
contents as time passed.

## What the rules read, in one place

```text
submission ──> TimeEntryFacts ──> TimeEntryRuleSet ──> [] or violations
                    ↑
       store: other minutes that date, matter + client + timekeeper active flags, current rate
       clock: today
```

The arrow into the facts is the whole design. The domain owns every rule and reaches for nothing;
the layer permitted to touch a database is the layer that gathers what the rules need.
