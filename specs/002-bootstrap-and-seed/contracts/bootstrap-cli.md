# Contract: `scripts/Initialize-LocalDb.ps1`

**Feature**: 002-bootstrap-and-seed
**Covers**: FR-001 to FR-011, FR-023, FR-024, FR-025 | **Verified by**: SC-001, SC-002, SC-003, SC-009

**Status**: carried forward from the original `001-local-environment-schema` planning run,
before the P3 split. `/speckit-plan` for this feature should validate it rather than
re-derive it.

The primary interface this feature exposes to a human. Its behaviour is a contract
because the README's quickstart depends on it exactly (FR-024, constitution P18).

---

## Invocation

```powershell
pwsh ./scripts/Initialize-LocalDb.ps1
```

No arguments required for the default path (FR-001).

| Parameter | Type | Effect |
|---|---|---|
| `-Reset` | switch | Drops and recreates the database, then migrates, applies procedures and reseeds. Does **not** stop, remove or rebuild the container (FR-006). Never prompts — the switch is the confirmation (FR-007) |
| `-SkipSeed` | switch | Brings the environment up and migrates, but does not generate data. For iterating on schema changes |

Full teardown of the container and its storage is **not** a parameter. It is
`docker compose down -v`, documented in the README (FR-008) rather than reimplemented
here.

---

## Steps and order

Order is part of the contract; a later step depends on every earlier one.

1. **Verify prerequisites.** Container tooling responding; the pinned SDK resolvable.
2. **Start the container** if it is not already running.
3. **Wait for readiness** by polling with an actual query until success or a bounded
   deadline (FR-009).
4. **Apply migrations.**
5. **Apply stored procedures** from `db/programmability/*.sql` in sorted order. An empty
   directory is a normal state, not an error.
6. **Seed** unless already seeded or `-SkipSeed` was supplied.
7. **Verify the seed** against the distribution bands in SC-004 and SC-007 and report
   each result.
8. **Print a development token** (FR-024).

---

## Idempotency

A second run against a complete environment performs no destructive action and leaves row
counts unchanged (FR-003, FR-004, SC-003). Each step reports whether it acted or skipped:

```
[1/8] Prerequisites .............. ok (Docker 27.x, SDK 9.0.317)
[2/8] Container .................. already running
[3/8] Readiness .................. ready in 0.4s
[4/8] Migrations ................. up to date, 0 applied
[5/8] Stored procedures .......... no procedures to apply
[6/8] Seed ....................... skipped, database already seeded (400,132 entries)
[7/8] Verification ............... 6/6 checks passed
[8/8] Development token .......... printed below
```

"Skipped" must be distinguishable from "done" in the output. A script that silently
reports success either way gives a developer no way to tell a working environment from a
no-op.

---

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Environment is complete and verified |
| `1` | A prerequisite is missing — container tooling not running, SDK not resolvable |
| `2` | Readiness deadline passed without the database accepting a query |
| `3` | Migration or procedure application failed |
| `4` | Seeding failed, or post-seed verification found a distribution outside its band |

Non-zero exits must name the cause in one plain sentence before any stack trace
(FR-011, SC-009). A stack trace alone is not a compliant failure.

---

## Failure messages

Each of these is an edge case named in the spec and must produce a message that identifies
it:

| Condition | Message must identify |
|---|---|
| Container tooling not running | That the tooling is unavailable, not a connection timeout |
| Port already in use | The conflicting port number |
| Readiness deadline exceeded | The timeout and how long it waited |
| Migration failure | Which migration failed |
| Partial seed detected on a later run | That a `-Reset` is required, rather than proceeding |
| Wrong SDK | The required version, `9.0.317` |

---

## Development token output

Printed on success, to stdout, at the end of the run. Signed with the same symmetric
development key the API validates against (research.md R5), with an expiry long enough to survive an
evaluation session (FR-025).

Never written to a file inside the repository, and never committed. The script prints it;
the reviewer pastes it into the Swagger authorize box.

---

## Acceptance

| Given | When | Then |
|---|---|---|
| Cold machine, image present | Script runs | Exit `0`, seeded database, token printed, under 3 min (SC-002) |
| Complete environment | Script runs again | Exit `0`, row counts unchanged, steps report "skipped" (SC-003) |
| Complete environment | Script runs with `-Reset` | Exit `0`, database dropped and rebuilt, container never restarted |
| Complete environment | Script runs without `-Reset` | No data dropped under any circumstance |
| Container tooling stopped | Script runs | Exit `1`, message names the tooling |
| `db/programmability/` empty | Script runs | Step 5 reports "no procedures to apply", exit unaffected |
