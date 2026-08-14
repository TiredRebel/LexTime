# Contract — the time-entry endpoints

Five routes under `/api/v1/time-entries`, from `docs/prd.md` §4. Registered by
`app.MapTimeEntryEndpoints()` (P21).

**All five require authentication.** They inherit feature 001's fallback-closed policy and add no
`AllowAnonymous`; `/health` and `/swagger` remain the only open routes (FR-023).

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/time-entries` | List, filtered and paged |
| `GET` | `/api/v1/time-entries/{id}` | One entry |
| `POST` | `/api/v1/time-entries` | Record — all rules apply |
| `PUT` | `/api/v1/time-entries/{id}` | Revise — rules apply per the update column of [domain-rules.md](./domain-rules.md) |
| `DELETE` | `/api/v1/time-entries/{id}` | Remove outright |

---

## `POST /api/v1/time-entries`

```jsonc
{
  "userId": 7,
  "matterId": 42,
  "workDate": "2026-08-12",
  "durationMinutes": 90,
  "isBillable": true,
  "narrative": "Reviewed the settlement agreement and marked up clause 7."
}
```

**No `hourlyRateSnapshot` field.** The rate is captured from the timekeeper, never supplied — a
caller able to state the rate could bill at any figure they liked, and rule 6 would be decoration.

### `201 Created`

The recorded entry, with its identifier and its captured rate, and a `Location` header. Returning
both means the caller need not re-fetch to learn either (FR-003).

```jsonc
{
  "timeEntryId": 400123,
  "userId": 7,
  "matterId": 42,
  "workDate": "2026-08-12",
  "durationMinutes": 90,
  "isBillable": true,
  "hourlyRateSnapshot": 450.00,
  "narrative": "Reviewed the settlement agreement and marked up clause 7.",
  "createdAtUtc": "2026-08-14T09:14:22.117Z",
  "updatedAtUtc": null
}
```

### `400 Bad Request` — a rule was broken

`application/problem+json`, with the rule and the offending value in the extension members so a
client can act without parsing the sentence.

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Domain rule violated",
  "status": 400,
  "detail": "Duration 7 is not a positive multiple of 6 minutes.",
  "violations": [
    { "rule": "DurationIncrement", "offendingValue": "7",
      "detail": "Duration 7 is not a positive multiple of 6 minutes." }
  ]
}
```

**All broken rules are listed, not just the first.** A submission wrong in three ways should not
take three round trips to fix.

`400` rather than `422`: every violation here is a well-formed request the domain refuses, and
the repository already uses `400` with `ProblemDetails` throughout. One status for one meaning is
worth more than a finer taxonomy nobody branches on.

### `404 Not Found`

A `userId` or `matterId` matching nothing. Distinct from a rule violation — an inactive matter
exists and is refused with a rule; a matter that does not exist is a different mistake and
deserves a different answer.

---

## `PUT /api/v1/time-entries/{id}`

Carries the whole entry, minus the two fields that are not revisable.

```jsonc
{
  "matterId": 42,
  "workDate": "2026-08-12",
  "durationMinutes": 120,
  "isBillable": true,
  "narrative": "Reviewed the settlement agreement; marked up clauses 7 and 12."
}
```

**No `userId` and no `hourlyRateSnapshot`.** Moving an entry between timekeepers would change
whose daily total it counts against and whose rate it should have captured, and neither has a
defined answer — re-record it instead. The rate is rule 6.

### Which rules apply

Rules 1, 2, 3 and 6 always. Rule 4 **only if `workDate` differs from the stored value**; rule 5
**only if `matterId` differs**. See the clarification in [spec.md](../spec.md) — an update that
leaves a field alone is not a submission of that field.

The practical consequence, which the tests aim at directly: an entry recorded 200 days ago can
still have its narrative corrected, and cannot have its date moved.

### `200 OK`

The revised entry, with `updatedAtUtc` now set.

### `400`, `404`

As above. **A refused revision leaves the stored entry exactly as it was** (FR-015) — a partially
applied update is worse than a refused one, and the test asserts the stored row is byte-identical
after a rejection.

---

## `DELETE /api/v1/time-entries/{id}`

### `204 No Content`

Gone. Not visible in any listing or in the rollup, and the identifier is not reused.

**Deletion is not gated by the backdating window or the matter's status** (FR-017). `docs/prd.md`
§2.2 rules out a locking workflow, and restricting removal on a period rule would be the first
half of one.

### `404 Not Found`

No entry with that identifier — including one already deleted. Deleting twice is not an error the
second time in any useful sense, but reporting `404` says plainly that there is nothing there.

---

## `GET /api/v1/time-entries`

| Parameter | Type | Required | Notes |
| --- | --- | --- | --- |
| `userId` | `int` | no | |
| `matterId` | `int` | no | |
| `from` | `date` | no | Inclusive lower bound on work date |
| `to` | `date` | no | Inclusive upper bound |
| `skip` | `int` | no | Default `0` |
| `take` | `int` | no | Default `50`, maximum `200` |

Filters combine with AND. All are optional — but an unfiltered request is still bounded by the
default page size and never returns the whole table (FR-020).

**Ordered by identifier, always.** Not by work date: the seed has thousands of entries per date,
so date alone is not a total order and two rows may tie — at which point successive pages can
drop one row and repeat another. The identifier is unique and monotonic (research R9).

### `200 OK`

```jsonc
{
  "skip": 0,
  "take": 50,
  "total": 1284,
  "items": [ /* TimeEntryDto */ ]
}
```

`total` is the count matching the filters, not the page — a caller cannot page sensibly without
it. `items` is `[]` for a range with nothing in it, which is a result and not an error.

A `take` of `0` or above the maximum is bounded rather than honoured literally; a negative `skip`
is treated as `0`.

---

## `GET /api/v1/time-entries/{id}`

`200` with the entry, carrying its captured rate. `404` if there is none.

---

## Not in this contract

- **No `PATCH`.** Not in §4's list, and "null means leave alone" makes clearing a nullable field
  inexpressible.
- **No bulk submission.** The interesting rules are per entry.
- **No ownership check.** `docs/prd.md` §2.2 rules out RBAC; a token here proves the caller is
  trusted, not who they are. Any caller may record time for any timekeeper.
- **No soft delete, no audit trail.** `CreatedAtUtc` and `UpdatedAtUtc` only.
