# Contract: `GET /health`

**Feature**: 001-solution-and-schema
**Covers**: FR-019, FR-023, FR-024, FR-025, FR-026, FR-027 | **Verified by**: SC-004, SC-006

The only externally reachable interface this feature adds. Unauthenticated by design —
it and the API documentation are the two exceptions to the bearer-token requirement.

---

## Request

```
GET /health
```

No authentication. No parameters. No request body. A bearer token, if supplied, is
ignored rather than rejected.

---

## Response

**Status codes**

| Code | Meaning |
|---|---|
| `200` | Every check passed |
| `503` | At least one check failed |

A degraded system returns 503. It must never return 200 with a failure described in the
body (FR-024) — that is the shape that makes automated probes report a dead service as
healthy.

**Body** — `application/json`, present on both status codes:

```jsonc
{
  "status": "Healthy",              // Healthy | Unhealthy
  "totalDurationMs": 12.4,
  "checks": [
    {
      "name": "database",
      "status": "Healthy",          // Healthy | Unhealthy
      "durationMs": 11.8,
      "description": null           // populated with the failure reason when Unhealthy
    }
  ]
}
```

Failure case:

```jsonc
{
  "status": "Unhealthy",
  "totalDurationMs": 2043.1,
  "checks": [
    {
      "name": "database",
      "status": "Unhealthy",
      "durationMs": 2042.6,
      "description": "Connection could not be established."
    }
  ]
}
```

Every check is listed by name in both cases, so a reader can tell **which** component
failed from the response alone, without log access (FR-025).

---

## Checks

| Name | What it verifies |
|---|---|
| `database` | That a trivial query **executes** successfully against the configured database |

The database check must execute a query, not merely construct a connection (FR-026).
Constructing a connection object succeeds against a server that is not running, so a
check that stops there reports healthy while the database is down — the exact failure the
requirement exists to prevent.

---

## Error disclosure

The `description` field may name the class of failure ("connection refused", "login
failed"). It must not contain the connection string, credentials, server hostname, or a
stack trace. This endpoint is unauthenticated; anything it returns is public.

---

## Timing

The check must reflect a state change within 5 seconds in both directions (SC-004). The
database check therefore carries a connection and command timeout short enough that a
failing check returns inside that window rather than waiting on a default timeout.

---

## Acceptance

| Given | When | Then |
|---|---|---|
| Database reachable | `GET /health` | `200`, `status: Healthy`, `checks[]` contains `database` as Healthy |
| Database container stopped | `GET /health` | `503`, `status: Unhealthy`, `database` check named as Unhealthy with a description |
| Database stopped, then restarted | `GET /health` within 5 s of each transition | Status follows the transition in both directions |
| No token supplied | `GET /health` | `200` — never `401` |
| Any other route, no token | `GET <route>` | `401` |
