# Data Model — Clients, Matters and Timekeepers (006)

**No schema change. No migration.** Every table, column, constraint and index is exactly as
features 001 and 004 left them. What this feature adds is a write path for two of the three
entities and a read path for all three.

A migration appearing here would mean something has gone wrong.

## What already exists, and what this feature does with it

### `Clients` — becomes writable

| Column | This feature |
| --- | --- |
| `ClientId` | assigned by the database; returned on creation so the caller need not look it up |
| `ClientCode` | **set once, never changed** (FR-011). Unique across the firm via `UX_Clients_ClientCode` |
| `Name` | set on creation, changeable afterwards |
| `IsActive` | true on creation, changeable in both directions |
| `CreatedAtUtc` | set on creation, never touched again |

`ClientCode` is the field the whole conflict path exists for. It is how the firm refers to the
client outside this system, which is why it is immutable and why a collision has to be answerable
rather than fatal.

### `Matters` — becomes writable

| Column | This feature |
| --- | --- |
| `MatterId` | assigned by the database |
| `ClientId` | set on creation from the route, **never changed** — moving a matter between clients would move its billing history with it |
| `MatterNumber` | **set once, never changed** (FR-011). Unique *within its client* via `UX_Matters_ClientId_MatterNumber` |
| `Name` | set on creation, changeable |
| `IsBillableByDefault` | set on creation, changeable |
| `IsActive` | true on creation, changeable in both directions |
| `CreatedAtUtc` | set on creation, never touched |

`ClientId` being immutable is not in the spec's requirement list because no endpoint offers it:
the matter is created under a client's route and the revise command has no client field. Recorded
here so its absence reads as a decision.

### `Users` — stays read-only

Exposed for reading and nothing else. `DefaultHourlyRate` is the value feature 005's rule 6
captures onto each entry at the moment it is recorded, and an editable rate would need the rate
history `docs/prd.md` §2.2 rules out. **SC-009 asserts no endpoint can create or modify one**,
which is a claim about the absence of routes rather than about a check inside one.

### `TimeEntries` — not touched

Read by one assertion only: that entries recorded before a matter was closed still appear in the
rollup afterwards (FR-014). Nothing in this feature writes to it.

## Application types

In `LexTime.Application/Parties/`.

### Commands

| Type | Carries | Deliberately absent |
| --- | --- | --- |
| `RegisterClientCommand` | code, name | active flag — a new client is active |
| `ReviseClientCommand` | name, active flag | **code** (FR-011) |
| `OpenMatterCommand` | number, name, default billable flag | client — it comes from the route; active flag — a new matter is active |
| `ReviseMatterCommand` | name, default billable flag, active flag | **number, client** (FR-011) |

The "deliberately absent" column is the enforcement. A command without a field cannot carry it,
so there is nothing for a handler to remember to ignore — the same technique feature 005 used to
make the rate snapshot unrewritable.

### Queries

| Type | Carries |
| --- | --- |
| `ListClientsQuery` | optional active filter, skip, take |
| `ListMattersQuery` | client, skip, take |
| `ListTimekeepersQuery` | skip, take |

All three clamp their window with feature 005's defaults and maximum, and all three order by
identifier (R8).

### Results

`PartyWriteResult` — one type for both aggregates, carrying an outcome and the record when there
is one.

| Outcome | Meaning | Becomes |
| --- | --- | --- |
| `Succeeded` | created or revised | `201` or `200` |
| `Conflict` | the code or number is taken; carries which field and which value | `409` |
| `NotFound` | the record, or the client a matter is being opened under, does not exist | `404` |

Three outcomes for three different mistakes, each with a different fix. Collapsing them would
force a caller to parse prose to work out whether to choose another code, correct a typo, or
create the parent first.

### DTOs

`ClientDto`, `MatterDto`, `TimekeeperDto`, each with a `ToDto()` extension beside it (P4).
`TimekeeperDto` carries the current rate; it is the only place in the API that exposes it.

## The two uniqueness rules, precisely

```text
Clients:  ClientCode                     unique across all clients
Matters: (ClientId, MatterNumber)        unique within one client
```

The second is composite, and that is the point. Two clients may each hold a matter numbered
`001`; one client may not hold two. Feature 002's generator restarts matter numbering per client
specifically so this index is exercised at volume, and a "matter numbers are unique" reading
would break the seeded dataset and every report drawn from it.

Both are enforced by the database and **stay** enforced by it. This feature translates the
resulting error into an answer (R2); it does not re-implement the rule. SC-011 writes a colliding
row from outside the application and asserts the constraint still refuses it — the same re-proof
feature 005 made for the duration constraint, for the same reason: adding an application-level
answer is exactly when someone concludes the database rule is redundant.

## Case sensitivity

`ACME` and `acme` collide, because the database collation is `SQL_Latin1_General_CP1_CI_AS` —
verified, not assumed. FR-006 makes that a stated rule rather than an accident of configuration,
and a test asserts it so a collation change fails loudly instead of silently permitting two
spellings of one client (R3).

## State

Two states per record, and no more: active or not. There is no draft, no pending, no archived,
and no transition rules — `docs/prd.md` §2.2 rules out an approval workflow, and feature 005's
rule 5 reads the flag directly.

```text
active ⇄ inactive          both directions, any number of times
```

Deactivating a client changes **one row** (FR-013, R7). Its matters keep their own flags, which is
what makes feature 005's two-branch refusal — "the matter is not active" versus "the matter is
active but its client is not" — reachable in both directions.
