# Research — Clients, Matters and Timekeepers (006)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected. Where a claim
about the database is made, it was **run** against the SQL Server 2022 container rather than
recalled.

---

## R1. The two uniqueness constraints already exist, and are named

**Verified** by reading feature 001's configurations:

| Constraint | Index name | Shape |
| --- | --- | --- |
| Client code, unique across the firm | `UX_Clients_ClientCode` | single column |
| Matter number, unique within a client | `UX_Matters_ClientId_MatterNumber` | composite, `(ClientId, MatterNumber)` |

The composite one is the interesting half. It permits the same matter number under different
clients, which the seed deliberately exercises — feature 002's generator restarts numbering at
`001` per client precisely so this index is under load at volume. FR-007 must preserve that, and
a naive "matter numbers are unique" reading would break the seeded dataset and everything
reporting on it.

Nothing about the schema changes in this feature. The constraints are made *answerable*, not
introduced.

---

## R2. Collisions are caught from the constraint, not pre-checked

**Decision.** Attempt the insert. When it fails, inspect the error and translate it into a
conflict naming the field. **No lookup before the write.**

**Verified** against the running container, by attempting to insert a lower-cased copy of an
existing client code:

```
probe: refused: err=2601 idx-in-msg=yes
```

Two facts established: the violation raises **error 2601**, and **the index name appears in the
message**. Translation can therefore key on `UX_Clients_ClientCode` versus
`UX_Matters_ClientId_MatterNumber` and say which rule was broken.

**Rationale.** A check-then-insert is a race: two requests can both find the code free and both
proceed. Closing that race needs a transaction or a lock, which is machinery bought to avoid a
`catch` that is still required — because the constraint can fail whatever the check said. One
path, no race, and the database stays the single arbiter of a rule it already owns.

FR-008's requirement is that a collision must not surface as a raw storage failure. It does not
require the collision to be predicted, only answered.

**Matching on the index name is locale-safe**, which matters more than it looks. SQL Server
localises its message text, so matching on English words like "duplicate key" breaks on a server
running in another language. The index name is a parameter substituted into the message and
survives translation. Error 2627 is checked alongside 2601 — the two differ by whether the
uniqueness came from a constraint or an index, and a future schema edit could switch which one
is raised.

**Alternatives rejected.**

- *Pre-check only.* Racy, and would report "available" a moment before the insert fails anyway.
- *Pre-check plus catch.* What most codebases do. Adds a query per create and a second code path
  to keep in agreement with the first, and removes no failure mode.
- *Matching English message text.* Works until the server is not English.

---

## R3. Case-insensitivity is a property of the schema, and gets a test rather than a second check

**Verified**:

```
db_collation:  SQL_Latin1_General_CP1_CI_AS
col_collation: SQL_Latin1_General_CP1_CI_AS
```

`CI` — case-insensitive. `ACME` and `acme` already collide, and the probe in R2 confirmed it by
being refused when it inserted a lower-cased duplicate.

**Decision.** Rely on the collation. Do **not** add an application-level case-folding check. Add
a test that asserts a differently-cased duplicate is refused.

**Rationale.** FR-006's concern is that a reader should not have to know the collation to predict
the API. A redundant `ToUpperInvariant()` comparison would satisfy that by duplicating a rule the
database already enforces — and would then disagree with the database the moment either changed.
A test states the rule in a place a reader looks and fails if the collation is ever altered,
which is the outcome FR-006 actually wants.

This is the same shape as P6's defence in depth read in reverse: where the database can enforce a
rule completely, the application's job is to *assert* it, not to re-implement it.

---

## R4. Immutability is expressed by the field being absent

**Decision.** `ReviseClientCommand` carries a name and an active flag. `ReviseMatterCommand`
carries a name, a default billable flag and an active flag. **Neither carries a code or a
number.**

**Rationale.** FR-011 requires that the change cannot be silently applied. A command without the
field cannot carry it, so there is nothing to ignore and no way to be inconsistent about
ignoring it. The same technique feature 005 used for the rate snapshot, and for the same reason:
a rule enforced by the shape of a type cannot be forgotten by a handler.

The consequence FR-012 records: **collisions are reachable only on creation.** There is no
update-side conflict path to write or test, and its absence is a design outcome rather than a
gap.

---

## R5. Three ports, and the cost of the fourth

**Decision.** `IClientStore`, `IMatterStore`, `ITimekeeperStore`, alongside feature 005's
`ITimeEntryStore`.

**Rationale.** Unchanged from feature 005's R2: `Application` cannot see `Infrastructure`, so a
use case reaches storage through an interface it declares, and P4's ban is on a *generic*
repository over `DbSet<T>` rather than on a per-aggregate port.

**What is worth saying now that was not worth saying then:** this is the fourth. The pattern is
no longer a one-off justified by a single reporting need — it is what P4's layering costs on
every aggregate, and it will cost the same on the next. Three of the four have methods that only
forward.

Recorded rather than re-argued. If the cost is judged too high, the fix is a constitution
amendment to P4, not a quiet exception inside a feature — and P2 and the Governance clause both
say so.

**Alternatives rejected.**

- *One combined `IPartyStore`.* Fewer files, at the price of a type whose name means nothing and
  which grows a method per aggregate forever.
- *Reusing `ITimeEntryStore`.* It already carries a domain query for rule 3; adding client CRUD
  would make it the generic repository P4 actually bans.

---

## R6. The deactivation boundary is tested from the other side

**Decision.** `DeactivationBoundaryTests` records time against a matter, closes the matter
through *this* feature's endpoint, and asserts feature 005's write path now refuses it — then
asserts feature 003's rollup still reports the entry recorded before the closure.

**Rationale.** This feature's only externally visible effect on behaviour is what it does to two
other features. A test that only checked `IsActive` came back `false` would prove the flag was
written and nothing about what it means.

The second half matters as much as the first. FR-014 requires deactivation to leave recorded time
alone, and a plausible wrong implementation — filtering closed matters out of the report — would
pass every test that only looked at the write path. The rollup assertion is what makes "closing a
matter does not erase its history" a checked claim rather than an intention.

**Also asserted: the two branches of rule 5 are reachable from here.** Closing a *matter* must
produce the matter-inactive refusal, and closing a *client* while leaving its matter open must
produce the client-inactive one. Feature 005 built both messages; until this feature there was no
way to cause the second, and untested reachable code is how a branch quietly stops working.

---

## R7. Deactivating a client does not touch its matters

**Decision.** Setting a client inactive changes exactly one row.

**Rationale.** FR-013, decided in the spec rather than asked, and the reasoning is worth keeping
next to the code: feature 005's rule 5 reads both flags and its refusal names which one failed.
A cascade would make the client-inactive branch unreachable, because a matter under a closed
client would itself always be closed. The seeded data already contains active matters of inactive
clients — feature 005's own quickstart walk hit one — so a cascade would also contradict the
dataset every other feature is measured against.

**Alternative rejected.** *Cascade on deactivate.* Intuitive, and it destroys a distinction the
previous feature deliberately built and tested.

---

## R8. Ordering and paging follow feature 005

**Decision.** All three listings order by identifier and clamp their page window with the same
defaults and maximum feature 005 established.

**Rationale.** The reason is the same one and is worth not relearning: ordering by a non-unique
column leaves ties the engine may resolve differently between requests, so paging can drop one
row and repeat another while the counts still look right. Client codes are unique and would
serve, but using the identifier everywhere means one rule to remember instead of three.

---

## R9. Conflicts answer 409, missing parents answer 404

**Decision.**

| Situation | Answer |
| --- | --- |
| Code or number already taken | `409 Conflict`, problem document naming the field and value |
| Client named by a matter creation does not exist | `404 Not Found` |
| Record fetched, revised or listed under does not exist | `404 Not Found` |
| Empty or whitespace code, number or name | `400 Bad Request` |

**Rationale.** `409` is the status whose meaning is "well-formed, and conflicts with current
state", which is exactly this. It also keeps a conflict distinguishable from the `400` this API
returns for a malformed request and from feature 005's `400` for a domain-rule refusal — three
different mistakes with three different fixes, and a caller that has to parse prose to tell them
apart has been given a worse API than one that answers with a status code.

**Rejected**: `400` for everything, which is what the rest of the API does. Consistency is worth
something, but not at the cost of collapsing "you sent nonsense" and "someone else already has
that code" into one answer.
