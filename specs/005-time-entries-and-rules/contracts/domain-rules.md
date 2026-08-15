# Contract — the six domain rules

`docs/prd.md` §2.1 states these in prose. This is the testable form: each rule's exact boundary,
what refusing it must say, and when it applies on update.

**Every rule is expressed once**, in `TimeEntryRuleSet` (FR-011). If a rule appears in a handler
or an endpoint, that is a defect regardless of whether it agrees with this table.

## The rules

### Rule 1 — duration is a positive multiple of six minutes

| | |
| --- | --- |
| **Refuses** | `0`, `-6`, `7`, `1`, `59` |
| **Accepts** | `6`, `12`, `600`, `1440` |
| **Boundary** | `6` accepted, `0` refused. There is no separate rule for zero or negatives — they are not positive multiples of six, and adding one would be two rules where §2.1 has one |
| **On update** | Always |
| **Message must name** | the submitted value and the six-minute increment |

Six minutes is a tenth of an hour. The rule exists because legal billing is quoted in tenths, and
an entry that cannot be expressed as one cannot be invoiced.

### Rule 2 — a single entry may not exceed 24 hours

| | |
| --- | --- |
| **Refuses** | `1446`, `2880` |
| **Accepts** | `1440`, `1434` |
| **Boundary** | `1440` accepted, `1446` refused — the next legal value above the limit, not `1441`, which rule 1 would refuse first |
| **On update** | Always |
| **Message must name** | the submitted value and the 1,440-minute maximum |

The boundary case is worth stating precisely: testing `1441` proves nothing about rule 2, because
rule 1 rejects it for a different reason. The first value that isolates rule 2 is `1446`.

### Rule 3 — a timekeeper's total for one date may not exceed 24 hours

| | |
| --- | --- |
| **Refuses** | 1,398 already recorded plus a 60-minute submission |
| **Accepts** | 1,398 already recorded plus a 42-minute submission |
| **Boundary** | a total of exactly `1440` is accepted; `1446` is refused |

**Every figure in a rule-3 case must itself be a legal duration.** An earlier draft of this
contract used 1,400 and 40, which look reasonable and prove nothing: neither is a multiple of six,
so rule 1 refuses the submission before rule 3 is reached. This is the same trap as testing rule 2
with 1441, one rule further along — and it was caught by the accepting test failing, which is
precisely what the accepting half of each pair is for.
| **On update** | Always, **excluding the entry being revised from the total** |
| **Message must name** | the minutes already recorded for that date and the maximum |

Counts every entry for that timekeeper and date, billable or not — the flag decides what is
charged, not what is possible.

**The exclusion on update is not an optimisation.** Counting an entry against itself makes
lowering a duration fail: an entry of 600 on a day totalling 1,440 could not be reduced to 300,
because 1,440 + 300 exceeds the limit. The test that catches this reduces a duration on a full
day.

**Concurrency.** Two submissions that each pass alone must not both succeed when together they
exceed the limit. The read and the write share a serialisable transaction (research R4).

### Rule 4 — the backdating window

| | |
| --- | --- |
| **Refuses** | tomorrow; 91 days ago |
| **Accepts** | today; 90 days ago; yesterday |
| **Boundary** | day 90 accepted, day 91 refused; today accepted, tomorrow refused |
| **On update** | **Only when the work date is being changed** |
| **Message must name** | the submitted date, today's date, and the 90-day limit |

Both ends are inclusive. "Not in the future" includes today, which is the common case and would
be an absurd thing to refuse.

**Today is supplied, never read.** The rule takes the current date as a fact. Every test of this
rule computes its dates relative to a fixed clock, so the suite says the same thing in December
as it does today (FR-026, SC-009).

**On update, the trigger is that the submitted date differs from the stored one.** An entry
recorded 200 days ago may still have its narrative corrected; it may not have its date moved,
even to another date 200 days ago.

### Rule 5 — an active matter belonging to an active client

| | |
| --- | --- |
| **Refuses** | an inactive matter; an active matter whose client is inactive |
| **Accepts** | an active matter of an active client |
| **On update** | **Only when the matter is being changed** |
| **Message must name** | **which of the two was inactive** |

The two cases are separate values on the facts, not one combined flag, because the refusal has to
distinguish them. A caller told only "not active" cannot tell whether to reopen a matter or a
client, and a message a caller cannot act on has failed at its only job (FR-008).

The seed guarantees both cases exist — roughly 10–15% of matters and clients are inactive, with
history intact — so these tests use real fixtures rather than fabricating one per test.

### Rule 6 — the rate is captured at recording and never rewritten

| | |
| --- | --- |
| **Refuses** | nothing. This rule cannot be violated by a submission |
| **Accepts** | — |
| **On update** | the stored value is carried forward untouched |
| **Proved by** | recording an entry, changing the timekeeper's rate, updating the entry, and asserting the captured rate is unchanged |

Unlike the other five, rule 6 is a statement about what the system does rather than about what a
caller may send. There is no input that breaks it and therefore no refusal to test.

**It is enforced structurally**: the revise command has no rate field, so the API offers no way to
change it. Its accepting test is the one above, and it is the test that would catch an update
handler that rebuilt the entity from scratch and re-read the current rate — a mistake that would
otherwise be silent and would rewrite history on every edit.

## The seventh check — an inactive timekeeper

Not one of the six `docs/prd.md` §2.1 lists. Added as FR-013 on the same reasoning as rule 5, and
flagged as an addition in the spec's checklist rather than slipped in: without it, someone who has
left the firm can still log time.

| | |
| --- | --- |
| **Refuses** | an entry recorded against an inactive timekeeper |
| **Accepts** | an active timekeeper |
| **On update** | never — the timekeeper cannot be changed by an update |

## Evaluation contract

- **All violated rules are returned, not just the first.** A submission wrong in three ways
  should not require three round trips to discover it.
- **The order is stable** — the enumeration's order — so a test can assert the whole collection.
- **No violations means no violations.** An empty result is the accepting case and must be
  distinguishable from "not evaluated".
- **Evaluation is pure.** No clock, no connection, no state. Given the same facts it returns the
  same violations, on any machine, forever.

## Test obligation

`docs/prd.md` §6.4 requires **every rule with a refusing test and an accepting test**. Twelve
minimum, and zero rules covered by only one of the two.

The reason is worth restating: a rule proved only to refuse could be refusing everything. The
accepting test is what distinguishes an enforced rule from a broken endpoint, and it is the half
that is easy to skip because it looks like it is testing nothing.
