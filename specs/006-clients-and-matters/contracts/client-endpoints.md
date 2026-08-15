# Contract — client endpoints

Four routes under `/api/v1/clients`, from `docs/prd.md` §4. Registered by
`app.MapClientEndpoints()` (P21). All require authentication and carry no `AllowAnonymous`.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/clients` | List, filtered by status and paged |
| `POST` | `/api/v1/clients` | Register a new client |
| `GET` | `/api/v1/clients/{clientId}` | One client |
| `PUT` | `/api/v1/clients/{clientId}` | Correct a name, or open and close |

---

## `POST /api/v1/clients`

```jsonc
{ "clientCode": "ACME", "name": "Acme Holdings" }
```

No `isActive` — a newly registered client is active, and offering the field would invite
registering something already closed.

### `201 Created`

```jsonc
{
  "clientId": 61,
  "clientCode": "ACME",
  "name": "Acme Holdings",
  "isActive": true,
  "createdAtUtc": "2026-08-15T09:14:22.117Z"
}
```

With a `Location` header. The identifier is returned because every subsequent call needs it.

### `409 Conflict` — the code is taken

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Client code already in use",
  "status": 409,
  "detail": "A client with code 'ACME' already exists.",
  "conflictingField": "clientCode",
  "conflictingValue": "ACME"
}
```

**`409`, not `400`.** The request is well-formed; it conflicts with state. Feature 005 already
uses `400` for a domain-rule refusal and this API uses `400` for a malformed request — three
different mistakes with three different fixes, and a caller should not have to read prose to tell
them apart.

**Case-insensitive.** Registering `acme` when `ACME` exists conflicts. The database collation
enforces this; a test asserts it so a collation change fails loudly rather than quietly admitting
two spellings of one client.

**The collision is detected by attempting the write**, not by looking first. A check-then-insert
is a race and does not remove the need for the catch it was meant to avoid (research R2).

### `400 Bad Request`

An empty or whitespace-only code or name. Checked before uniqueness — a malformed request and a
conflicting one are different answers.

---

## `PUT /api/v1/clients/{clientId}`

```jsonc
{ "name": "Acme Holdings Ltd", "isActive": false }
```

**No `clientCode`.** It is immutable after creation (FR-011) and the field is absent rather than
ignored, so there is nothing a caller can send that is silently discarded. A code is how the firm
refers to this client on invoices and in correspondence; changing it breaks every reference held
outside this system.

**Consequence**: there is no conflict path on this route. `409` is unreachable here, and its
absence is a design outcome rather than an oversight (FR-012).

### `200 OK`

The revised client.

### What deactivation does

Sets `isActive` to `false` on **this row only**. Its matters keep their own flags (FR-013).

That is what makes feature 005's two refusals distinguishable: closing a *matter* produces "the
matter is not active", and closing a *client* while its matter stays open produces "the matter is
active but its client is not". A cascade would make the second unreachable — and the seeded data
already contains active matters of inactive clients.

**Recorded time is untouched.** Entries against a closed client keep appearing in the weekly
rollup exactly as before (FR-014). Deactivation stops new billing; it does not erase history, and
a test asserts the rollup's figures for that client are unchanged across the closure.

### `404 Not Found`

No client with that identifier.

---

## `GET /api/v1/clients`

| Parameter | Type | Required | Notes |
| --- | --- | --- | --- |
| `isActive` | `bool` | no | Omitted returns both |
| `skip` | `int` | no | Default `0`; negative treated as `0` |
| `take` | `int` | no | Default `50`, maximum `200` |

Ordered by identifier — the same rule feature 005 uses, because ordering by a non-unique column
lets successive pages drop one row and repeat another while the counts still look right.

### `200 OK`

```jsonc
{ "skip": 0, "take": 50, "total": 61, "items": [ /* ClientDto */ ] }
```

`total` is the count matching the filter, not the page. An unfiltered request is still bounded by
the default page size and never returns every client.

---

## `GET /api/v1/clients/{clientId}`

`200` with the client, `404` if there is none.

---

## Not in this contract

- **No `DELETE`.** `docs/prd.md` §2.2 has no such endpoint, and time entries reference clients
  through their matters — removing one would either orphan entries or destroy billing records.
  Deactivation is the domain's own answer and the one the schema was built for.
- **No code change.** See above.
- **No cascade of any kind.**
- **No ownership check.** Any authenticated caller may register or close any client; §2.2 rules
  out RBAC.
