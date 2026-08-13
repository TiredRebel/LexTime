# Contract: `LexTime.Api` command-line surface

**Feature**: 002-bootstrap-and-seed
**Covers**: FR-002, FR-003, FR-005, FR-010, FR-023, FR-024 | **Realises**: research.md R0, R1, R5

The application gains maintenance verbs so the bootstrap script has something to call. This
is the mechanism that keeps the seeder out of a fifth project (P4) and keeps `dotnet ef`
out of the quickstart (P18).

---

## Invocation

```powershell
dotnet run --project src/LexTime.Api --no-launch-profile -- <verb> [options]
```

`--no-launch-profile` is **not optional**. `dotnet run` otherwise honours
`Properties/launchSettings.json` and forces the Development environment regardless of
`ASPNETCORE_ENVIRONMENT` — during feature 001 this made a Production check appear to pass
when it had never run in Production. The script passes it and sets the environment
explicitly.

---

## Verbs

| Verb | Purpose | Exit 0 means |
|---|---|---|
| `migrate` | Apply all pending EF Core migrations | The schema is at the latest migration |
| `apply-procedures` | Execute every `db/programmability/*.sql` in sorted order | All files applied, or none existed |
| `seed` | Generate and bulk-load the dataset | The dataset is loaded |
| `verify-seed` | Run the distribution checks | Every band was met |
| `state` | Report whether the database is empty, complete or partial | State written to stdout |
| `mint-token` | Print a development bearer token | Token written to stdout |

With **no verb**, the host starts the web server exactly as it does today. This path must
stay untouched: all twenty-one existing tests host the application through
`WebApplicationFactory` with no arguments, and would fail if argument handling changed the
default.

---

## Options

| Option | Verbs | Effect |
|---|---|---|
| `--entries <n>` | `seed` | Override the time-entry count. Tests use this to seed at 1/100 scale |
| `--reset` | `migrate` | Drop and recreate the database before applying migrations |

Volumes other than `--entries` are not exposed. They are constants in `SeedOptions`, and a
knob nobody turns is a knob that rots.

---

## Behaviour

**`migrate`** resolves `LexTimeDbContext` from the built host and calls `MigrateAsync()`.
Re-running when up to date is a no-op that exits 0 — the same guarantee `dotnet ef database
update` gives, without the tool.

**`apply-procedures`** enumerates `db/programmability/*.sql` sorted by filename and executes
each file's full contents. **An empty directory is success, not failure** — it is the
default state until feature 003, and the only state this feature will ever see. Each file
is authored `CREATE OR ALTER PROCEDURE`, so re-application never requires a drop (P7).

**`seed`** refuses to run against a database whose state is not `Empty`, so that seeding is
never accidentally additive. The script decides whether to reset; the host does not decide
for it.

**`state`** writes one of `Empty`, `Complete` or `Partial` and exits 0 in all three cases —
it reports, it does not judge. Judging is the script's job, because only the script knows
whether `-Reset` was requested.

**`mint-token`** signs with the configured key using the constants in `AuthenticationSetup`,
so the token cannot drift out of agreement with the validator. Claims are the minimum the
fallback policy needs — an authenticated identity and nothing implying an authorisation
model that does not exist.

---

## Exit codes

| Code | Meaning |
|---|---|
| `0` | The verb completed |
| `1` | Configuration is missing or invalid — no connection string, no signing key |
| `2` | The database could not be reached |
| `3` | The verb failed — migration error, procedure error, load error |
| `4` | `verify-seed` found a distribution outside its band |
| `5` | `seed` refused because the database was not empty |

Distinct codes exist so the script can act on them. Collapsing them to 1 would force the
script to parse messages.

---

## Output

Human-readable on stdout, one line per meaningful step. Not JSON: the only consumer is a
PowerShell script that checks exit codes, and a developer reading the terminal.

`verify-seed` prints one line per check with its measured value and band, then a summary:

```
weekend share ................ 4.9%    (band < 10%)      ok
non-billable share ........... 18.1%   (band 10-25%)     ok
top-10 client share .......... 57.3%   (band >= 50%)     ok
duration violations .......... 0       (band = 0)        ok
entries after reference date . 0       (band = 0)        ok
inactive clients ............. 11.7%   (band 10-15%)     ok
inactive w/ history .......... 3       (band >= 1)       ok
7/7 checks passed
```

Measured values are printed whether or not they pass. A check that only reports "ok" tells
a reader nothing about how close to a boundary the data sits.

---

## Acceptance

| Given | When | Then |
|---|---|---|
| Any state | Host started with no arguments | Web server runs; the 21 existing tests pass unchanged |
| Migrated, empty database | `seed` | Exit 0, dataset loaded |
| Already-seeded database | `seed` | Exit 5, nothing written |
| Empty procedure directory | `apply-procedures` | Exit 0, reports nothing to apply |
| Seeded database | `verify-seed` | Exit 0, seven checks printed with measured values |
| Deliberately skewed data | `verify-seed` | Exit 4, the failing check named |
| No connection string | any verb | Exit 1, message names the missing setting |
