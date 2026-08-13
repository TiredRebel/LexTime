# Agent log

What Claude Code got wrong while building this repository, what the symptom was, and how it
was caught. Required by constitution P16; at least three of these reach the README.

A repository that claims AI-assisted development and shows zero friction is not credible.
This is the friction.

---

## Specification phase

### 1. A spec that contradicted itself on dates

**Generated**: a spec requiring the seed to span 24 months *and* requiring every seeded
entry to satisfy the rule forbidding work dates more than 90 days in the past.

**Symptom**: none at the time. Both requirements read as reasonable in isolation, the
quality checklist passed at 16/16, and the spec was committed.

**Caught by**: the `/speckit-clarify` ambiguity scan, which asked which of the two governed
and exposed that roughly seven eighths of the dataset would violate the spec's own rule.

**Resolution**: creation-time rules separated from stored-data invariants. The 90-day limit
constrains submissions through the API; it is not an invariant on recorded history. The
consequence propagated into this feature as FR-012 — `WorkDate` carries no check constraint
— with a positive test asserting a three-year-old date is accepted, so that "fixing" the
apparent omission breaks a test rather than the seed.

### 2. A binding principle naming an unverified flag

**Generated**: constitution P23, mandating that the pipeline build with `--warnaserror`.
The flag was written from recall, not from a run.

**Symptom**: the first verification appeared to pass with zero warnings.

**Caught by**: noticing the build had been incremental and nothing had recompiled. Re-run
with `--no-incremental`, the flag correctly escalated CS0219 to an error and returned exit
1. The flag was real; the test that "confirmed" it had proved nothing.

**Resolution**: the flag stayed. The lesson did not — see entry 5 below, where the same
incremental-build trap caught the agent a second time in the same repository.

### 3. A rule its own next action would have violated

**Generated**: the first draft of constitution P22 read "`main` is never committed to
directly."

**Symptom**: none yet. The rule had not been committed.

**Caught by**: review before commit. The constitution's own Amendment clause requires
amendments in a dedicated commit with no branch involved, and the very next commit was an
amendment on `main`.

**Resolution**: narrowed to exempt governance commits. Feature work still requires a
branch.

### 4. Third-party licence terms stated from memory

**Generated**: the PRD's §2.3, asserting MediatR and AutoMapper licensing as fact, from
recall.

**Symptom**: plausible prose that would have shipped in a public README.

**Caught by**: checking the packages rather than trusting the recollection. The recalled
version was directionally right and missing the decisive detail: MediatR v13+ requires a
**registered licence key at runtime**, which would have put an account signup in front of
the two-command quickstart that constitution P18 requires.

**Resolution**: both libraries dropped. `Application` now has no third-party runtime
dependency. The rule against unverified performance numbers turned out to apply just as
well to licence claims.

### 5. A file written into the wrong repository

**Generated**: a copy of the PRD into `docs/prd.md` — in a different project.

**Symptom**: a new untracked file appeared in an unrelated Python repository.

**Caught by**: running `git status` and seeing it. The agent had already "verified" the
copy with a bare `git status`, which had reported success from the wrong working
directory, because the shell's working directory reset between calls.

**Resolution**: every subsequent git and file operation uses an absolute path.

---

## Implementation phase

### 6. An XML comment that broke the build system

**Generated**: `Directory.Build.props` with a comment reading ``the pipeline builds with
`--warnaserror` ``.

**Symptom**: every build failed with `NuGet.targets: Invalid framework identifier ''` —
a message pointing at NuGet, TargetFramework and restore, none of which were the problem.

**Caught by**: not guessing. Running `dotnet msbuild -getProperty:TargetFramework` on a
single project reported the real error: `MSB4024: An XML comment cannot contain '--'`. The
props file had failed to import, so nothing in it applied, so `TargetFramework` was empty.

**Resolution**: comment rephrased. The lesson is about the misleading error, not the XML
rule — two rounds of plausible guesses (stale `obj`, missing property) would have been
wrong, and the diagnostic took one call.

### 7. The incremental-build trap, a second time

**Generated**: `dotnet ef migrations script --no-build`, to review the generated SQL before
committing it as constitution P15 requires.

**Symptom**: the script contained only the migrations-history table. No `Clients`, no
`TimeEntries`, no check constraint.

**Caught by**: reading the output instead of skimming it. `--no-build` had used an assembly
compiled before the migration existed.

**Resolution**: regenerated with a real build. The review then found what it was supposed
to find and confirmed all five things it was checking for. Notable that this is the same
class of mistake as entry 2, in the same repository, a day apart — a stale artefact
reporting success.

### 8. A primary-constructor parameter shadowed into nonexistence

**Generated**: `DatabaseHealthCheck(LexTimeDbContext context)` with a method
`CheckHealthAsync(HealthCheckContext context, ...)`, referring to `this.context` in the
body.

**Symptom**: `CS9113: Parameter 'context' is unread` alongside `CS1061: does not contain a
definition for 'context'` — the compiler reporting both that the parameter was unused and
that it did not exist.

**Caught by**: the compiler, immediately.

**Resolution**: constructor parameter renamed to `dbContext`. Worth recording because the
error pair is confusing on a first read: `this.` does not reach a primary-constructor
parameter, and the method parameter had shadowed it.

### 9. A test that failed on its own fixture

**Generated**: `TokenFactory.CreateExpired()`, producing a token with an expiry one hour in
the past and a `notBefore` five minutes in the past.

**Symptom**: `IDX12401: Expires must be after NotBefore`. The test failed while
constructing the token, before any request was made.

**Caught by**: the test run. The failure was real but pointed at the wrong thing — it
proved the helper was wrong, not that the boundary rejected expired tokens.

**Resolution**: `notBefore` anchored to the expiry rather than to now. Worth recording
because a passing suite is not evidence a test tests anything; this one would have "passed"
as a failure for the right-looking reason if the assertion had been on the exception type.

### 10. Package versions selected ahead of the target framework

**Generated**: `dotnet add package Microsoft.EntityFrameworkCore.SqlServer` with no version.

**Symptom**: `NU1202: Package ... 10.0.11 is not compatible with net9.0`. The command
resolved the newest published version, which targets .NET 10.

**Caught by**: the restore failing loudly.

**Resolution**: all framework packages pinned to `9.*`. Recorded because the same command
succeeded silently for `Microsoft.Extensions.Diagnostics.HealthChecks`, adding a 10.0.11
reference that would have been a latent problem rather than an obvious one — that package
was removed and the health check written against the framework's own abstractions instead.

---

## Security review — feature 001 (constitution P24)

Manual review of the token validation configuration and the one analyzer suppression,
carried out before commit. P24 requires this for any change touching auth or SQL.

**Verified good:**

- The signing key comes from configuration and is never hard-coded. Startup throws a named
  exception if it, the issuer or the audience is missing, so a misconfigured environment
  fails immediately rather than rejecting every caller at runtime.
- The accepted algorithm is pinned to HMAC-SHA256 via `ValidAlgorithms` rather than
  inferred from the token's own `alg` header. A validator that trusts the header lets the
  caller choose the algorithm.
- Issuer, audience, lifetime and signing-key validation are each enabled explicitly rather
  than left to defaults.
- `ClockSkew` is set to zero. The five-minute default would accept a token that expired
  four minutes ago — and would have made the expired-token test pass for the wrong reason.
- Key length is checked against the 32-byte minimum for HMAC-SHA256, with a message naming
  the actual length. A short key otherwise throws deep inside the validator on first use.
- The authorisation fallback policy closes every endpoint by default. Anonymous access is
  opt-in and appears exactly twice: the health check and the API documentation.
- **Fails closed outside Development.** Verified by actually running it:
  `ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile` exits 82 with
  `Connection string 'LexTime' is not configured`, so the committed development key cannot
  be picked up by a non-development environment. The first run of this check was invalid —
  `dotnet run` honoured `launchSettings.json` and started in Development regardless of the
  environment variable, and the app came up listening. `--no-launch-profile` was needed to
  test what was actually being claimed.

**Accepted risks, recorded rather than fixed:**

- The development signing key is committed in `appsettings.Development.json`. This is the
  stated trade-off in PRD §2.3 and the README, and it is bounded by the fail-closed
  behaviour above.
- Swagger UI is served unconditionally in every environment. PRD §4 makes `/swagger`
  anonymous by design; it would need revisiting if this were ever deployed anywhere real,
  which the pipeline explicitly does not do.
- The health check's failure description is fixed text, not the provider's exception
  message, because the endpoint is unauthenticated and a provider message can name a host.
  Asserted by `DoesNotLeakConnectionDetails_WhenTheDatabaseIsUnreachable`.

**The one suppression in the codebase:** `CA2100` in `tests/.../DirectSql.cs`, where a
`SqlCommand` is constructed from a `string` parameter. Reviewed: every call site passes a
compile-time literal, and all user-supplied values go through `SqlParameter`. The
suppression is scoped to the two lines that need it and carries its justification inline.

---

## Not a mistake, but worth recording

**EF Core's generated migrations do not satisfy this repository's own analyzer settings.**
`AnalysisMode=Recommended` flagged CA1062, CA1861 and IDE0161 in the scaffolded migration.
Hand-fixing them would be undone by the next `migrations add`. Resolved by declaring
`**/Migrations/*.cs` as `generated_code = true` in `.editorconfig` — the mechanism the
analyzers provide for exactly this — with a comment recording that it suppresses style and
quality rules only, and that the SQL those files emit is still read line by line before
commit, which is what P15 actually asks for.

Notably, **CS1591 did not need suppressing**: EF writes an `auto-generated` header the
compiler already honours. The documentation gate reaches every line of hand-written code in
the repository, including every test method.
