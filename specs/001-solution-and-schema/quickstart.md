# Quickstart Validation: Solution and Schema

**Feature**: 001-solution-and-schema | **Date**: 2026-08-12

How to prove this feature works end to end. Every scenario maps to a success criterion in
[spec.md](./spec.md); contract detail lives in
[contracts/health.md](./contracts/health.md) and is referenced rather than repeated.

**Three commands, not two.** The two-command quickstart promised in `docs/prd.md` §6.3 is
[feature 002](../002-bootstrap-and-seed/spec.md). Until that lands, bringing the database
up and applying the schema are separate manual steps, and the README says so rather than
promising a script that does not exist.

---

## Prerequisites

- Docker (or an equivalent container runtime) running
- .NET SDK 9.0.317 or a later 9.0.x — pinned in `global.json`

---

## Scenario 1 — Clean build *(SC-002)*

```powershell
dotnet build --warnaserror
```

**Expected**: succeeds with zero diagnostics across all five projects. Any analyzer finding
or any publicly visible member without an XML documentation comment fails here — that is
what makes constitution P23 and P25 enforcement rather than convention.

---

## Scenario 2 — The documentation gate actually fires *(SC-003)*

Add a public method with no `<summary>` to any project — including the test project — and
rebuild.

**Expected**: build failure citing **CS1591**. Remove it and confirm the build recovers.

Run this once, deliberately. A gate nobody has seen fire is a gate nobody knows is wired
up, and the test project is the one most likely to have been quietly exempted.

---

## Scenario 3 — Database up and schema applied *(SC-001, SC-007)*

```powershell
docker compose up -d
```

```powershell
dotnet ef database update --project src/LexTime.Infrastructure --startup-project src/LexTime.Api
```

**Expected**: four tables in schema `dbo` with their constraints and no rows.

Run the migration command a second time. **Expected**: succeeds and changes nothing
(SC-007).

---

## Scenario 4 — Service runs and health is green *(SC-001)*

```powershell
dotnet run --project src/LexTime.Api
```

```powershell
curl http://localhost:5000/health
```

**Expected**: `200`, `status: Healthy`, with a `database` check listed by name. Full shape
in [contracts/health.md](./contracts/health.md).

---

## Scenario 5 — Health reflects reality *(SC-004)*

With the API running, stop the database container and request health within 5 seconds:

```powershell
docker compose stop
curl -i http://localhost:5000/health
```

**Expected**: `503`, `status: Unhealthy`, the `database` check named as the failing one
with a description. Restart the container and request again: `200` within 5 seconds.

The failure case is the one that matters. A check that constructs a connection without
executing a query returns `200` here and passes a naive test — FR-026 exists to prevent
exactly that.

---

## Scenario 6 — Storage constraints reject bad data *(SC-005)*

Run these directly against the database, with no application involved. Bypassing the
application is the point: constitution P6 requires the database to hold this line
independently, and feature 002's bulk load will not go through application validation.

```sql
-- all four rejected by the CHECK constraint
INSERT INTO dbo.TimeEntries (..., DurationMinutes, ...) VALUES (..., 7, ...);     -- not a multiple of 6
INSERT INTO dbo.TimeEntries (..., DurationMinutes, ...) VALUES (..., 0, ...);     -- not positive
INSERT INTO dbo.TimeEntries (..., DurationMinutes, ...) VALUES (..., -6, ...);    -- negative
INSERT INTO dbo.TimeEntries (..., DurationMinutes, ...) VALUES (..., 1446, ...);  -- over 1440

-- accepted
INSERT INTO dbo.TimeEntries (..., DurationMinutes, ...) VALUES (..., 6, ...);
```

Uniqueness, including the case most likely to be modelled wrongly:

```sql
-- rejected: duplicate client code, duplicate email
-- accepted: matter number '001' under client A AND under client B
-- rejected: matter number '001' twice under client A
```

And the constraint that must **not** exist:

```sql
-- accepted: a billing date three years in the past
INSERT INTO dbo.TimeEntries (..., WorkDate, ...) VALUES (..., '2023-08-12', ...);
```

**Expected**: that last insert succeeds. If it fails, someone has added a date constraint,
and feature 002's seed will not load. FR-012 is a requirement that something be absent,
which is the easiest kind to break by "fixing".

---

## Scenario 7 — Access boundary *(SC-006)*

```powershell
curl -i http://localhost:5000/health                                              # expect 200
curl -i http://localhost:5000/api/v1/ping                                         # expect 401
curl -i -H "Authorization: Bearer <test-minted token>" http://localhost:5000/api/v1/ping   # expect 200
curl -i -H "Authorization: Bearer not-a-token" http://localhost:5000/api/v1/ping           # expect 401
```

**Expected**: health open, protected route closed without a token, open with a valid one,
closed again with a malformed one. The third case matters as much as the second — without
it, a route that rejects everything unconditionally would pass.

The token here is minted by the test project using the development key. The
reviewer-facing token printed by a script arrives with feature 002.

`/api/v1/ping` is a temporary placeholder that exists only to prove the boundary. It is
removed when the first real endpoint lands and is not one of the seventeen.

---

## Scenario 8 — Dependency direction *(SC-008)*

Open `src/LexTime.Domain/LexTime.Domain.csproj`.

**Expected**: no `ProjectReference` and no `PackageReference` to any persistence, web or
serialisation package. The check takes fifteen seconds and is the single fastest way for a
reviewer to confirm the layering is real rather than decorative.

---

## Automated tests

```powershell
dotnet test
```

Runs against a real SQL Server container via Testcontainers (constitution P11). Covers
Scenarios 5, 6 and 7. Scenarios 1, 2, 3, 4 and 8 are validated by hand — see the P13
reasoning in [plan.md](./plan.md).
