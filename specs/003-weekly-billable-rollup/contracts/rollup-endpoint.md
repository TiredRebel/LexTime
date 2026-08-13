# Contract — `GET /api/v1/reports/weekly-billable-rollup`

The headline endpoint of `docs/prd.md` §4. Registered by `app.MapReportEndpoints()` (P21).

Authentication is **required**. The endpoint inherits feature 001's fallback-closed
authorization policy and adds no `AllowAnonymous`; `/health` and `/swagger` remain the only
open routes (FR-017).

## Request

| Parameter | In | Type | Required | Notes |
| --- | --- | --- | --- | --- |
| `from` | query | `date` (`YYYY-MM-DD`) | **yes** | Inclusive |
| `to` | query | `date` (`YYYY-MM-DD`) | **yes** | Inclusive |
| `clientId` | query | `int` | no | Restricts rows; changes no figure inside a row |

```
GET /api/v1/reports/weekly-billable-rollup?from=2026-01-05&to=2026-03-29
Authorization: Bearer <token>
```

Neither date has a default. Omitting one is a 400, not a request for "everything" or "the last
quarter" — a report that silently picks its own range is worse than one that refuses
(FR-018).

## Responses

### `200 OK`

```jsonc
{
  "from": "2026-01-05",
  "to": "2026-03-29",
  "rows": [
    {
      "isoYear": 2026,
      "isoWeek": 3,
      "weekStartDate": "2026-01-12",
      "clientId": 7,
      "clientCode": "ACME",
      "clientName": "Acme Holdings",
      "billableHours": 128.40,
      "nonBillableHours": 11.20,
      "billableAmount": 48150.00,
      "cumulativeBillableHours": 371.90,
      "hoursDeltaVsPriorWeek": 14.70,   // null when the prior week is outside the range
      "clientRankInWeek": 2
    }
  ]
}
```

Field meanings are in [data-model.md](../data-model.md); the SQL types they come from are in
[usp-weekly-billable-rollup.md](./usp-weekly-billable-rollup.md).

`rows` is `[]` — never `null`, never absent — when the range contains no activity or the
client filter matches nothing. Both are successes, not errors (FR-020). A client identifier
that matches no client is **not** a 404: the report is over a period, and a client with nothing
in that period legitimately produces nothing.

`hoursDeltaVsPriorWeek` is `null` only when the preceding calendar week falls outside the
requested range. A week the client was silent through, inside the range, gives the week's full
hours. Consumers that treat `null` as zero will misreport every client's first week.

### `400 Bad Request`

`application/problem+json` (RFC 7807), consistent with the rest of the API (FR-019).

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid reporting range",
  "status": 400,
  "detail": "'from' (2026-03-29) must not be later than 'to' (2026-01-05)."
}
```

Triggers: `from` later than `to`; either date missing; either date unparseable. The message
names the offending values — SC-008 is judged by someone who has not read the code.

### `401 Unauthorized`

No token, malformed token, expired token, or a token signed with a key the host does not
trust. No report data is returned under any parameter combination (SC-006). These four cases
are already covered by `AuthBoundaryTests`, which retargets from the removed `/api/v1/ping`
placeholder to this route (R9).

## Not in this contract

Stated so the boundary reads as chosen (PRD §2.2):

- **No pagination.** The response is bounded by the data — at most 6,240 rows for the full
  seeded history (SC-004). Paging a bounded report adds a cursor protocol for no benefit.
- **No caching headers, no ETag, no rate limiting.** There is no load to justify them.
- **No CSV or Excel export.** JSON only, as the whole API is.
- **No `groupBy` or `orderBy` parameters.** The ordering is part of the procedure's contract
  and is what makes SC-003 checkable.

## Behaviour a test must be able to demonstrate

1. A valid request over the seeded range returns rows with all twelve fields populated.
2. `hoursDeltaVsPriorWeek` is `null` on the first reported week of the range and a number
   afterwards.
3. A client filter returns only that client's weeks, with `clientRankInWeek` still reflecting
   its position among all clients — not `1` on every row.
4. An empty range returns `200` with `"rows": []`.
5. `from` later than `to` returns `400` naming both dates.
6. No token returns `401` and no body containing report data.
