# Contract — matter and timekeeper endpoints

Six routes from `docs/prd.md` §4, registered by `app.MapMatterEndpoints()` and
`app.MapTimekeeperEndpoints()` (P21). All require authentication.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/clients/{clientId}/matters` | Matters under one client |
| `POST` | `/api/v1/clients/{clientId}/matters` | Open a matter under that client |
| `GET` | `/api/v1/matters/{matterId}` | One matter |
| `PUT` | `/api/v1/matters/{matterId}` | Correct a name, or open and close |
| `GET` | `/api/v1/users` | List timekeepers |
| `GET` | `/api/v1/users/{userId}` | One timekeeper |

Matters are created under a client's route and fetched by their own. That asymmetry is
deliberate: a matter cannot exist without a client, so creation states the parent in the path,
while a matter's own identifier is globally unique and needs no context to resolve.

---

## `POST /api/v1/clients/{clientId}/matters`

```jsonc
{ "matterNumber": "001", "name": "Merger — Phase 1", "isBillableByDefault": true }
```

No `clientId` in the body — it is in the route. No `isActive` — a new matter is active.

### `201 Created`

The matter, with its identifier and its client's, plus a `Location` header.

### `409 Conflict` — the number is taken **under this client**

```jsonc
{
  "title": "Matter number already in use for this client",
  "status": 409,
  "detail": "Client 61 already has a matter numbered '001'.",
  "conflictingField": "matterNumber",
  "conflictingValue": "001"
}
```

**This is a composite rule and the detail must say so.** The same number under a *different*
client is accepted and must be — feature 002's generator restarts matter numbering at `001` for
every client precisely so the composite index is exercised at volume, and a global reading would
break the seeded dataset and every report drawn from it.

A message saying only "matter number already in use" would send a caller looking for a matter
that is not theirs.

### `404 Not Found` — the client does not exist

Distinct from `409`. A missing parent and a taken number are different mistakes: one is fixed by
creating the client, the other by choosing another number. Answering both the same way makes the
caller guess.

### `400 Bad Request`

Empty or whitespace-only number or name, checked before uniqueness.

---

## `PUT /api/v1/matters/{matterId}`

```jsonc
{ "name": "Merger — Phase 1 (revised)", "isBillableByDefault": true, "isActive": false }
```

**No `matterNumber` and no `clientId`.** The number is immutable (FR-011); the client is immutable
because moving a matter between clients would move its entire billing history with it, and
nothing in `docs/prd.md` asks for that. Both are absent from the command rather than ignored, so
neither can be silently discarded.

**Consequence**: no conflict path on this route either.

### `200 OK`

The revised matter.

### What deactivation does

Sets `isActive` on this row. New time recorded against it is refused by feature 005's rule 5,
with a refusal naming the **matter**. Time recorded before the closure is untouched and still
appears in the weekly rollup (FR-014).

`isBillableByDefault` is a default for new entries, not a retroactive statement: entries already
recorded keep their own billable flag. Feature 001's schema comment says the same thing from the
other side — writing off an hour on a billable matter is an ordinary act.

### `404 Not Found`

No matter with that identifier.

---

## `GET /api/v1/clients/{clientId}/matters`

| Parameter | Type | Notes |
| --- | --- | --- |
| `skip` | `int` | Default `0` |
| `take` | `int` | Default `50`, maximum `200` |

Returns only that client's matters, active and inactive alike, ordered by identifier. A client
with no matters returns an empty page — a result, not an error. A client that does not exist
returns `404`, which is a different thing and says so.

---

## `GET /api/v1/users` and `/api/v1/users/{userId}`

```jsonc
{
  "userId": 7,
  "email": "a.novak@lextime.test",
  "fullName": "A. Novak",
  "defaultHourlyRate": 450.00,
  "isActive": true
}
```

Paged and ordered by identifier, like the others.

### Read-only, and provably so

**There is no `POST` and no `PUT` on either route** — not a check inside a handler, but the
absence of a route. `docs/prd.md` §2.1 makes timekeepers seeded and read-only, and §2.2 rules out
the rate history an editable rate would require.

`defaultHourlyRate` is the value feature 005's rule 6 captures onto each time entry at the moment
it is recorded. Exposing it here is what lets a caller predict what an entry will be billed at;
it is not an invitation to change it. Feature 005's own rule-6 test alters a rate directly in the
database for exactly this reason, and that remains the only way.

**SC-009 asserts this**, by requesting the routes that do not exist and confirming they are not
served. An assertion about absence is easy to skip and is the only thing standing between "we
decided not to" and "we forgot".

---

## Not in this contract

- **No `DELETE` for matters.** Same reasoning as clients: time entries reference them.
- **No matter-level or client-level rates.** `docs/prd.md` §2.2 — one captured rate on the entry
  proves the point.
- **No moving a matter between clients**, and no merging or renumbering. Real operations in a
  real firm, and far past a demo.
- **No creating or editing timekeepers**, by any route, under any circumstances.
