# Research — Time Entries and the Domain Rules (005)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected.

---

## R1. The rules are one pure evaluation, and the domain is told its facts

**Decision.** `LexTime.Domain/Rules/TimeEntryRuleSet.cs` holds a single static evaluation taking
a `TimeEntryFacts` record and returning the violations. It reads no clock, opens no connection,
and knows nothing about how its facts were obtained.

Split by what each rule needs:

| Rule | Needs | Source |
| --- | --- | --- |
| 1 — six-minute increment | the duration alone | the submission |
| 2 — 24-hour maximum | the duration alone | the submission |
| 3 — daily maximum | the timekeeper's other minutes that date | a query, supplied as a number |
| 4 — backdating window | today's date | the clock, supplied as a value (R3) |
| 5 — active matter and client | both active flags | a lookup, supplied as two booleans |
| 6 — rate snapshot | the timekeeper's current rate | a lookup, supplied as a value |

**Rationale.** Constitution P6 puts the rules in the domain and P4 forbids the domain any
persistence reference, and only four of the six are pure. Passing the facts in resolves that
without weakening either: the domain still owns every rule, and the layer that can reach a
database is the layer that does.

It also makes the rules exhaustively testable with no container. Twelve tests — a refusal and an
acceptance for each rule — plus every boundary, run in milliseconds against a record. A rule
suite that needed a database for each case would be slow enough that nobody would grow it.

**Alternatives rejected.**

- *Rules as methods on the `TimeEntry` entity.* Natural for rules 1 and 2 and impossible for 3,
  4 and 5, which need facts the entity does not hold. Splitting them by mechanism would put half
  the rules in one place and half in another, which is exactly what FR-011 forbids.
- *Rules in the handlers.* Fails P6 outright, and puts rule 3 in two handlers — create and
  revise — where they will eventually diverge.
- *A rules engine or specification pattern.* Six rules. Six `if` statements against a record is
  the readable answer, and a reviewer can check it against `docs/prd.md` §2.1 line by line.

---

## R2. The persistence port, weighed against P4's repository ban

**Decision.** `LexTime.Application/TimeEntries/ITimeEntryStore.cs`, implemented once by
`EfTimeEntryStore` in `Infrastructure`. Non-generic, one aggregate, with the operations the five
use cases need plus the daily-total query rule 3 depends on.

**This needs justifying rather than assuming, because P4 bans "a generic repository over
`DbSet<T>`"** on the grounds that "EF Core's `DbSet<T>` is already the repository and
`DbContext` is already the unit of work; wrapping them adds a layer that only forwards calls".

The honest position, both halves of it:

*Why it is not the thing P4 bans.* It is not generic — there is no `IRepository<T>`, no type
parameter, and it cannot be reused for clients or matters. It exists in exactly one copy. And
`SumMinutesForUserOnDateAsync` is not a `DbSet` operation at all; it is a domain question with an
aggregate answer, and it is the fact rule 3 cannot be evaluated without.

*Why the objection still partly lands.* `FindAsync`, `AddAsync` and `RemoveAsync` do forward. If
`Application` could see `LexTimeDbContext` they would not exist. They exist because P4's own
layering forbids that — and P4's own text says "an interface with a single implementation is
expected here rather than deferred". The forwarding is the price of the layering, charged by the
principle that also bans paying it twice.

**Alternatives rejected.**

- *Move the write use cases into `Infrastructure`* so they can use `DbContext` directly. Removes
  the port and breaks P4's "every use case is one handler class in `LexTime.Application`".
- *Let `Application` reference EF Core* and take `DbContext` directly. Puts a persistence package
  in the layer P4 keeps clean, and makes the handlers untestable without a database.
- *A generic `IRepository<T>`.* The thing actually banned. Would also have no home for the daily
  total.

---

## R3. The clock is injected, and its test double is five lines of our own

**Decision.** Handlers take `System.TimeProvider` and ask it for the current date. The test
project defines a `FixedClock : TimeProvider` overriding `GetUtcNow()`.

**Verified**: `TimeProvider` is an abstract type in the base class library with a virtual
`GetUtcNow()`. No package reference is added to use it or to subclass it.

**Rationale.** Rule 4 is a rule *about today*. Read the clock inside the rule and every test of
it is a test about the real calendar: `2026-06-01 is within the window` passes now and fails in
December. **FR-026 and SC-009 exist because a suite that rots on a date is worse than no suite —
it fails while nothing is wrong, and people learn to ignore the failure.**

`Microsoft.Extensions.TimeProvider.Testing` supplies `FakeTimeProvider` and would be the
conventional choice. Rejected: it is a package added to a repository whose whole quality argument
is that a reviewer needs nothing installed, in exchange for saving five lines. Overriding one
method is not a problem worth a dependency.

**Alternatives rejected.**

- *A hand-rolled `IClock` in `Domain`.* An interface the framework already provides, with a worse
  name and no ecosystem.
- *`DateTime.UtcNow` inside the rules.* The failure this whole item exists to prevent.

---

## R4. Rule 3 is a read-then-write, and is serialised

**Decision.** The daily-total query and the insert or update run inside one transaction opened at
`IsolationLevel.Serializable`.

**Rationale.** Rule 3 sums the timekeeper's other entries for the date, adds the submitted
duration, and compares. Two requests arriving together both read a total of 1,400, both add 40,
both pass, and the stored total becomes 1,480 — the rule refuses nothing and is satisfied by
neither request individually being wrong. The spec's edge case names this and requires it not be
defeasible by timing.

`Serializable` is what makes the read see a range that cannot change beneath it. `Repeatable
Read` is not enough: it holds the rows it read and does not stop a second transaction inserting a
new one, which is exactly the case here.

**The cost, stated rather than discovered.** There is no index on `(UserId, WorkDate)`, so the
range lock the serialisable read takes is coarser than it would be with one. At this repository's
write volume — one row at a time, no concurrent load — that is invisible. **An index is
deliberately not added**: the spec puts index changes out of scope, and a new index on this table
would perturb the measurement feature 004 just committed. If write contention ever mattered, the
narrower fix is `sp_getapplock` keyed on the timekeeper and date, which serialises the exact pair
being written instead of a range. Recorded as the upgrade path, not taken now.

**Alternatives rejected.**

- *Check without a transaction.* The bug this item exists to prevent, and it would pass every
  test written sequentially.
- *A database constraint.* A cross-row sum per `(UserId, WorkDate)` is not expressible as a
  `CHECK`. An indexed view could approximate it and is far more machinery than the rule is worth.
- *Optimistic retry on a computed column.* Same objection, plus a schema change.

---

## R5. "Being changed" means "differs from what is stored"

**Decision.** The update is a `PUT` carrying the whole entry, as `docs/prd.md` §4 specifies. The
handler loads the stored entry and compares field by field. Rule 4 is evaluated only when the
submitted work date differs from the stored one; rule 5 only when the submitted matter differs.

**Rationale.** The clarification settled *which* rules re-apply on update; this settles how the
code knows. With a `PUT`, every field is supplied, so "unchanged" cannot be inferred from
absence — it has to be a comparison. That turns out to be the better mechanism anyway: it needs
no nullable-means-untouched convention, no `PATCH` semantics, and no way for a caller to
accidentally clear a field by omitting it.

It also gives the two rules a precise trigger a test can aim at: submit an old entry unchanged
and it is accepted; submit the same entry with the work date moved by one day and rule 4 refuses
it, even though the new date is no further from today than the old one.

**Alternatives rejected.**

- *A `PATCH` with nullable fields.* Not in §4's endpoint list, and "null means leave alone" makes
  clearing a nullable field impossible to express.
- *Change flags on the command.* Asks the caller to declare intent the server can determine, and
  a caller that lies about it bypasses a rule.

---

## R6. A violation becomes a problem response carrying the rule and the value

**Decision.** `RuleViolation` carries the rule, the offending value and a sentence. The API maps
a violation to `400` with `application/problem+json`, putting the rule's name and the value in
the problem's extension members alongside the human-readable detail.

**Rationale.** FR-010 forbids "invalid request" as compliance, and SC-004 is judged by someone
who has not read the code and must be able to say what to change. A detail sentence satisfies a
human; a machine-readable rule name and value satisfies a client that wants to highlight the
offending field. Both are cheap once the violation is a value rather than an exception message.

**Violations are returned, not thrown.** A rejected submission is an ordinary outcome of a
correct request, not an exceptional condition. Throwing would make the handler's signature lie
about what it does and would push rule handling into middleware where it is invisible.

---

## R7. The duplication with the schema is the point, and gets re-proved

**Decision.** The `CHECK` constraint on duration stays exactly as feature 001 wrote it. The C#
checks for rules 1 and 2 are additional. A test writes a violating row **outside the
application** and asserts the database still refuses it.

**Rationale.** P6 calls this duplication "intentional defence in depth", and the failure mode it
anticipates is specific: someone later reads the C# check, concludes the constraint is redundant,
and removes it — at which point the bulk seeder, any manual fix-up and any future service lose
their last guard. SC-010 makes that removal fail a test rather than pass review.

Feature 001 already tested the constraint. This re-proof is not redundant with it: 001 proved the
constraint existed, and this proves it *still* bites after a second enforcement layer arrived,
which is precisely when it is most likely to be deleted.

---

## R8. The rate snapshot is written once and never recomputed

**Decision.** `RecordTimeEntryHandler` reads the timekeeper's current rate and writes it onto the
entry. `ReviseTimeEntryHandler` never touches that field — the stored value is carried forward
unchanged, and the update command has no field for it.

**Rationale.** Rule 6 is easy to satisfy on create and easy to break on update: an update handler
that rebuilds the entity from the command and re-reads the rate would silently rewrite history
on every edit, and no existing test would notice. Leaving the rate off the update command
entirely means the mistake cannot be made through the API at all.

The acceptance test is the one that would catch it if it were: record an entry, change the
timekeeper's rate, update the entry's narrative, and assert the captured rate is what it was.

---

## R9. Paging is ordered by the key, not by the work date

**Decision.** The listing orders by `TimeEntryId`. Filters narrow; the order does not change with
them.

**Rationale.** FR-021 requires paging that cannot skip or repeat. Ordering by `WorkDate` alone
does not give a total order — the seed has thousands of entries per date — so two rows tie and
the engine is free to return them in a different order on the next page, which drops one row and
duplicates another. The identifier is unique and monotonic, so the order is total and stable.

Ordering by date would read better and be wrong. If date order is ever wanted, the fix is
`(WorkDate, TimeEntryId)`, which is still total.

---

## R10. Two tiers of test, for two different reasons

**Decision.**

- **`TimeEntryRuleTests`** — no container, no database. Constructs facts and asserts the
  evaluation. Every rule refusing, every rule accepting, every boundary at the limit and one step
  past it.
- **`TimeEntryWriteTests` and `TimeEntryListingTests`** — through the endpoint against a real
  container, covering that the rules are actually reached, that a refusal leaves data untouched,
  and that the storage constraint still bites.

**Rationale.** P11 requires integration tests against real SQL Server and says nothing that
forbids fast tests as well; P13 asks for coverage weighted to what matters. The rule table is
where the exhaustive cases belong and where a container per case would make thoroughness
expensive. The endpoint tests exist to prove the wiring, not to re-enumerate the rules.

**The two tiers answer different questions**, and the distinction is worth keeping: the pure
tests ask *is the rule right*, the endpoint tests ask *is the rule reached*. A feature that
enforced every rule perfectly in a class nothing called would pass the first tier completely.

---

## Open questions

None. The spec's single `NEEDS CLARIFICATION` was closed in its clarification session before this
plan began, and no new one arose during design.
