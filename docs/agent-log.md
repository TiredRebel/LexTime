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

## Feature 002 — bootstrap and seed

### 11. A guard that fired before the handler meant to catch it

**Generated**: a maintenance verb layer mapping configuration failures to exit code 1, per
its own contract.

**Symptom**: running a verb with no connection string killed the process with an unhandled
`InvalidOperationException`, a full stack trace, and exit code `-532462766`. The documented
exit code 1 was unreachable.

**Caught by**: running the verb instead of trusting the `try`/`catch` that surrounded the
call. Feature 001's configuration guard throws during *service registration*, which happens
before the host is built and therefore before anything in the verb layer exists to catch
it.

**Resolution**: the guard moved up to wrap registration, and only for maintenance
invocations — a misconfigured web server should still fail loudly rather than start quietly.

### 12. Adding a return value silently broke nine tests

**Generated**: `return await MaintenanceCommands.RunAsync(...)` in `Program.cs`, so the verb
layer's exit code became the process exit code.

**Symptom**: nine tests failed with "The server has not been started or no web application
was configured". Every one of them was a `WebApplicationFactory` test; none of them touched
the code that changed.

**Caught by**: the regression check the task list required immediately after modifying
`Program.cs`, for exactly this reason.

**Resolution**: two separate causes, found in order. A `return <int>` anywhere in top-level
statements makes the generated `Main` return `Task<int>`, which the factory's entry-point
interception does not support — replaced with `Environment.ExitCode`. That alone did not fix
it: the gate was `args.Length > 0`, and **the test host passes arguments of its own**, so
every test was being treated as a maintenance invocation and exiting before the server
started. The gate is now "the first argument is a verb this class owns", which is what it
should have said in the first place.

### 13. A shell preference turned a success into a misleading failure

**Generated**: a bootstrap script with `$ErrorActionPreference = 'Stop'` and native commands
invoked as `& docker ... 2>&1`.

**Symptom**: `FAILED: Docker is not responding` — while Docker was running and serving
containers.

**Caught by**: not believing the message. `docker ps` from the same shell exited 0 and
listed the running container.

**Resolution**: with `Stop` in effect, redirecting a native command's stderr through `2>&1`
turns *any* stderr line into a terminating error, even when the command succeeded. Every
external call now goes through one helper that drops to `Continue` for the duration and
returns the exit code explicitly. The failure this caused is precisely the one FR-011 exists
to prevent: a message that names the wrong cause.

A second, smaller instance in the same area: the prerequisite check originally used
`docker version`, which can print a client version and exit 0 while the daemon is
unreachable. It answers a different question than the one being asked, so the check now uses
`docker ps`.

### 14. A regex replacement inlined the entire file into itself

**Generated**: a PowerShell `-replace` whose replacement text contained `$_`, intended as
part of a `Where-Object` block being written into the script.

**Symptom**: the script file roughly doubled in size and the second half of it appeared
partway through a line of the first.

**Caught by**: reading the diff. In .NET replacement syntax `$_` is a substitution meaning
**the entire input string**, so the whole file was inserted at that point.

**Resolution**: file rewritten wholesale rather than patched. Worth recording because the
mistake is invisible in the pattern — the replacement text looked like ordinary PowerShell.

### 15. The analyzers objected to the one thing that had to be true

**Generated**: a deterministic data generator built on `System.Random`.

**Symptom**: `CA5394: Random is an insecure random number generator`, six times, failing the
build under `AnalysisModeSecurity=All`.

**Caught by**: the build gate, immediately.

**Resolution**: the diagnostic is correct in general and inverted here — the requirement is
that two runs produce *identical* rows, which a cryptographic generator cannot do by
design. Suppressed at the class with a justification naming FR-020, and reviewed as a P24
item below. This is worth recording because "fix the analyzer warning" would have quietly
destroyed the property the index before/after measurement depends on.

---

## Feature 005 — time entries and the domain rules

### 22. A boundary value that tested the wrong rule

**Generated**: a rule-3 test asserting that 1,400 minutes already recorded plus a 40-minute
submission is accepted — 1,440 exactly, the daily maximum, so it should pass.

**Symptom**: it failed. The submission was refused.

**Cause**: **40 is not a multiple of six.** Rule 1 refused the entry before rule 3 was ever
consulted, so the test proved nothing about the daily maximum and its expected outcome was
simply wrong. 1,400 is not a legal total either, since it cannot be the sum of six-minute
increments.

**Caught by**: the accepting half of the pair. The refusing case passed throughout — rule 3 does
refuse 1,400 + 60 — so a suite testing only refusals would have been green and meaningless.
`docs/prd.md` §6.4 requires both halves for exactly this reason, and this is the first time in
this repository that requirement has paid out rather than merely been satisfied.

**Worth recording for where the error started.** This feature's own contract document specified
those numbers, and the contract *also* warned, one rule earlier, that rule 2 must be tested with
1446 rather than 1441 because 1441 trips rule 1 first. The same trap was described and then
walked into one row further down the same table. Both the test and the contract are now fixed,
and the contract says why.

### 23. A restored file that the build refused to notice

**Symptom**: after deliberately mutating `TimeEntryRuleSet` to return no violations and
confirming 14 of 29 tests failed, restoring the original file left the suite **still failing 14**
— with the mutation demonstrably gone from the source.

**Cause**: the restore was `Copy-Item` from a backup, which carries the backup's original
timestamp. That timestamp was older than the compiled assembly, so MSBuild judged the source
unchanged and skipped the rebuild. The tests ran against the mutated binary.

**Caught by**: not believing the result. The source said one thing and the test said another, and
only one of them can be right about what was compiled.

**Resolution**: `dotnet build --no-incremental`, after which the suite was immediately green.

**This is the third instance of this class in this repository** — the `--warnaserror` false pass
in feature 001, the stale `dotnet ef` script in feature 002, and now this. The pattern is always
the same: a build that reports success without having rebuilt, and a conclusion drawn from an
artefact older than the change. `--no-incremental` is in the task lists for this reason, and it
earned its place again.

### 24. The mutation check that replaced an ordering I skipped

The task list said to ship the rule set as a stub, write the tests against it, and implement
afterwards — so that the tests would fail by *accepting* everything, proving each one exercises a
rule rather than a missing type. **I wrote the rule set first and skipped that.**

Rather than claim the ordering was followed, the guarantee was recovered directly: with all 29
rule tests green, `Evaluate` was mutated to return no violations, and the suite re-run. **Fourteen
failed and fifteen passed — and the fifteen that passed were exactly the accepting cases**, which
should still pass when everything is accepted. Every refusing test therefore depends on the rule
it names.

That is a stronger check than the stub-first order would have produced, because it tests the
finished rules rather than an empty shell. It is recorded here because the ordering in the task
list was a real instruction and it was not followed; the outcome was recovered, not the process.

---

## Feature 004 — index and measured performance

### 19. A measurement that confidently reported zero

**Generated**: a reader loop that consumed the rollup's rows, hashed them, and disposed the
reader.

**Symptom**: the first full run produced a clean summary table in which **every logical read
count was zero** and every elapsed time was a single or double digit. Nothing failed. The
equivalence check passed. The plans were captured correctly, at 200 KB each, so the procedure
had plainly executed.

**Caught by**: the committed raw output — the very file that exists so the summary can be
audited rather than trusted. It contained the `STATISTICS TIME` lines for the `SET` statements
and nothing at all from the procedure: no `Table '...'. Scan count` lines, no execution time
that resembled the query. That gap named the cause almost immediately.

**Cause**: SQL Server sends `STATISTICS IO` and `TIME` output as info messages **after the final
result set**. A reader disposed while anything is still pending discards them. The loop read the
first result set, stopped, and threw the measurement away. The Phase 0 probe had not caught it
because that probe drained with `NextResultAsync` — for unrelated reasons, and the difference
went unnoticed when the loop was written.

**Resolution**: drain to the end of the stream before disposing, with a comment saying why,
because reading the first result set and stopping is the obvious way to write that loop.

**Worth recording for what nearly happened.** Zero logical reads for both index states is a
result that could have been written up — "the index makes no measurable difference to reads" is
a sentence someone could publish with a straight face, and the equivalence check would have
gone on passing underneath it. What prevented that was a rule adopted before the code existed:
commit the raw output verbatim next to the summary. The discipline caught the bug before the
bug reached the document.

### 20. Published figures that stopped matching their own evidence

**Symptom**: after `docs/performance.md` was written from the first good run, a second run was
made to confirm reproducibility. It confirmed it — identical read counts, identical hashes,
different milliseconds, exactly as claimed. It also **overwrote the raw capture files**. The
document's CPU figures now said 837 ms and 103 ms; the committed evidence beside it said 776 and
91.

**Caught by**: checking, rather than assuming. The reproducibility run was made specifically to
test a claim, and re-reading the files afterwards was part of testing it.

**Cause**: the reduced figures — medians, ranges — only ever existed on stdout. The per-reading
captures were committed and the reduction was not, so half the published table had no committed
source and nothing tied the two together. Re-running the measurement then silently de-linked the
rest.

**Resolution**: the verb now writes `summary.txt` alongside the raw captures, so a run's reduced
figures are committed with the evidence they came from. The document was rewritten from a single
authoritative run, and every published number was then verified to appear in a committed file
before the commit was made.

**The rule this violated was written by this feature, in its own contract, before any code
existed**: *every number comes from a committed raw file*. It was still broken within an hour of
being written — not by ignoring it, but by not noticing that the medians were never covered by
it. A provenance rule is only as good as the mechanism that keeps it true; being careful is not
a mechanism.

### 21. CA2100, predicted and then not suppressed

**Symptom**: the build failed with `CA2100` on a private helper in `RollupMeasurer` that took
its SQL as a `string` parameter.

**Cause**: passing a constant through a parameter hides it from the analyzer, which then
correctly reports that it cannot see where the text came from.

**Resolution**: the helper takes a constructed `SqlCommand` instead, so each call site builds it
from a constant the analyzer can see. **No suppression was added.**

Recorded not because it was hard, but because the plan predicted it. `research.md` R5 stated in
advance that a `CA2100` in this feature would be a design error rather than a finding to
justify, and the task list repeated it. When it appeared, the decision had already been made and
took no argument. Writing down what a future warning would *mean* turned out to be worth more
than writing down how to handle it.

---

## Feature 003 — weekly billable rollup

### 16. A contract fixed in one place and drifted from in another

**Generated**: the procedure's final `SELECT`, returning `DENSE_RANK() OVER (...)` directly as
`ClientRankInWeek`.

**Symptom**: every one of the eleven tests that read a row failed with
`System.InvalidCastException: Unable to cast object of type 'System.Int64' to type
'System.Int32'`, thrown from `SqlDataReader.GetInt32`. Nothing named the column, and the stack
pointed only at the reader's mapping loop.

**Cause**: SQL Server's ranking functions — `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `NTILE` — all
return `bigint`. The result-set contract says `int`, and a standing among sixty clients has no
use for the other thirty-two bits.

**Caught by**: the tests, immediately and loudly. Worth recording for *why* it was possible:
the feature deliberately shipped the procedure first as a typed empty shell, precisely so the
twelve-column contract was fixed before anything bound to it. The shell declared
`CAST(0 AS int)` and was right. The implementation that replaced it then drifted from the
contract the shell existed to establish — so the technique worked exactly as intended and the
mistake happened one layer above it.

**Resolution**: `CAST(... AS int)` on the rank, with a comment saying why the cast is there
rather than leaving a future reader to wonder.

### 17. Two findings from reading the SQL that no test would have produced

Constitution P15 requires generated SQL involving window functions to be read line by line
rather than accepted on a green run. It had just gone green on all fifty-eight tests. The read
found two things anyway, neither of which any test could fail on:

- **A cast that implied a subtlety that did not exist.** The `DENSE_RANK` ordered by
  `CAST(BillableMinutes AS decimal(18,4)) DESC`. Ranking by the integer is identical — the
  conversion is monotonic — so the cast bought nothing and suggested to a reader that something
  about the decimal form mattered. Removed.
- **An unstated ceiling.** The amount is summed as minutes-times-money and narrowed to
  `decimal(19,4)`, which holds about 10^15. That is roughly ten thousand times the seeded
  maximum, so no test will ever reach it — but it is a real ceiling and a widening is not free,
  because `decimal(38,x)` divided by a literal runs into SQL Server's precision rules and loses
  scale rather than erroring. Now stated in the procedure.

Neither is a bug. Both are the kind of thing that only turns up when someone reads the code
with the question "what is this actually claiming?", which is the whole argument for P15.

### 18. A principle that mandates an edge its own diagram omits

**Observed**: constitution P4 states the dependency rule as `Api → Application → Domain`, and
`Infrastructure → Domain`. It then requires, in the same paragraph, that "where `Application`
needs infrastructure it declares the interface and `Infrastructure` implements it".

**Symptom**: the second requirement cannot be satisfied while the first list is exhaustive.
`SqlWeeklyBillableRollupReader` implements `IWeeklyBillableRollupReader`, which lives in
`LexTime.Application`, so `LexTime.Infrastructure` must reference it — an edge P4's list does
not name.

**Caught by**: the compiler, on the first build of the reader. Recorded because the interesting
part is not the error but what it exposes: the Constitution Check at `/plan` passed this
feature on P4 without anyone noticing the tension, because the prose was read as governing and
the arrow list as illustrative. A gate that is checked by reading can be passed by reading it
the convenient way.

**Resolution taken**: the reference was added, with a comment in the `.csproj` pointing at the
plan. The dependency still points inward — `Application` is the more central layer and knows
nothing of `Infrastructure` — which is the rule P4's title states. **The arrow list should be
amended to name the edge** so the document matches the design it mandates; that is a governance
commit, not something to slip into a feature.

---

## The build gate caught a vulnerability nobody in this repository introduced

**Date**: 2026-08-13, while specifying feature 003. Not an agent mistake — recorded because
it is the clearest evidence that the P23/P24 gate does something, and it arrived without
anyone touching the code.

**Symptom**: a build that had been green the previous evening failed with
`NU1903: Package 'SSH.NET' 2025.1.0 has a known high severity vulnerability`. No source file
had changed. GitHub advisory GHSA-q939-rpr3-3284 had been published in the interval, and
`NuGetAudit` with `NuGetAuditMode=all` re-evaluates the whole transitive graph on every
build rather than only direct references.

**Cause**: `Testcontainers.MsSql 4.13.0` → `Docker.DotNet.Enhanced 4.3.3` → `SSH.NET`.
Three levels down, in a test-only dependency, on a code path this project never executes —
SSH.NET is how Testcontainers reaches a Docker daemon over SSH, and this project uses a
local one.

**Resolution**: 4.13.0 is the latest Testcontainers release, so there was no upstream fix to
take. Pinned SSH.NET to 2026.0.0 as a direct reference in the test project, which lifts the
transitive resolution out of the vulnerable range; audit went quiet, which is the advisory
database confirming the version is patched rather than an inference. Verified the pin does
not break the thing it sits underneath: `dotnet test` still passes 40 of 40 against real
containers. The reference carries a comment saying to delete it once Testcontainers ships
the patched version itself, so it does not calcify into a mystery.

**Worth noting**: the tempting response was to scope `NuGetAuditMode` back to `direct` and
make the message disappear. That would have silenced every future transitive finding as
well, including ones that matter, and it would have done so in a way no reviewer could see.
P24 exists to make that trade explicit rather than convenient.

### 25. The seed does not satisfy a rule this feature added — found by the rule refusing it

**Symptom**: a quickstart check submitted an entry dated 200 days ago, expecting one violation —
the backdating window. It came back with **two**:

> This would bring the timekeeper's total for 2026-01-27 to 2040 minutes, above the
> 1440-minute daily maximum. 1980 minutes are already recorded.

**Cause**: the seeded dataset routinely exceeds rule 3. **8,727 seeded user-days hold more than
1,440 minutes**, the worst of them 3,834 — a timekeeper billing sixty-four hours in one day. The
arithmetic makes it inevitable: 400,000 entries across 25 timekeepers and roughly 730 days is
about 22 entries per person per day, and at a realistic length that is well past a day.

**Is this a defect in feature 005?** No, and the distinction matters. The rules bind at recording
and at correction, not retroactively — this feature's Assumptions say so, and feature 002 settled
the same question for the backdating window. The API correctly refuses to *add* to an
already-overfull day while leaving the recorded history alone. Every rule behaved exactly as
specified.

**It is a realism defect in the seed, which this feature made visible.** Constitution P9 asks for
data that is realistic in shape, and sixty-four-hour days are not. Nobody would have noticed
before now, because nothing previously asked the question — the rollup aggregates by client and
week, where the anomaly disappears into a total.

**Deliberately not fixed here.** Changing the seed's shape is out of this feature's scope, and it
would invalidate feature 004's committed performance evidence: those figures were measured
against this dataset, and the whole argument for them is that a reviewer can regenerate the same
numbers from the same committed constants. Fixing the seed means re-taking that measurement, and
that belongs in a feature that says so. **Recorded here as a known inconsistency with an owner
rather than left for a reviewer to find.**

---

## Security review — feature 005 (constitution P24)

This feature adds the API's first write path and five new routes, so P24 applies twice over.
Reviewed before commit.

**Zero new analyzer suppressions**, and none needed. Every query on this path is EF Core LINQ
with parameters the provider supplies; the only hand-written SQL in the feature is in the tests.
The repository's existing suppressions are unchanged.

**What a caller cannot set.** Three fields are absent from the commands by design, and their
absence is the enforcement rather than a check:

- **The rate.** `RecordTimeEntryCommand` has no rate field. A caller able to state it could bill
  at any figure they chose, and rule 6 would be decoration. It is read from the timekeeper.
- **The rate again, on update.** `ReviseTimeEntryCommand` has no rate field either, so the API
  offers no way to rewrite history even accidentally.
- **The timekeeper, on update.** Moving an entry between people would change whose daily total it
  counts against and whose rate it should have captured. Neither has a defined answer, so the
  field does not exist.

**Identifiers come from the route, never the body.** `PUT` and `DELETE` take the entry's
identifier from the path; the body cannot name a different one. A body-supplied identifier is the
classic way an update becomes an update of something else.

**The access boundary.** All five routes are registered in one group with no `AllowAnonymous`, so
they inherit feature 001's fallback-closed policy. `/health` and `/swagger` remain the only open
routes.

**Two things accepted rather than fixed**, both recorded so they read as decisions:

- **No ownership model.** Any authenticated caller may record time for any timekeeper, including
  one who is not them. `docs/prd.md` §2.2 rules out RBAC and a real identity provider: the token
  proves the caller is trusted, not who they are. In a real system this would be a finding; here
  it is a stated boundary, and the endpoints do not pretend otherwise.
- **Serialisable isolation is a denial-of-service surface.** A caller can hold range locks on a
  timekeeper's date by submitting concurrently. There is no load and no untrusted caller, the
  transaction is short, and the alternative — dropping the isolation level — reintroduces the
  race rule 3 exists to close. Named rather than mitigated.

---

## Security review — feature 004 (constitution P24)

This feature executes DDL and a `DBCC` command, so P24 applies. Reviewed before commit.

**Zero new analyzer suppressions**, and one avoided deliberately. `CA2100` fired on a helper
that took its SQL as a parameter; the response was to restructure so the analyzer can see the
constants, not to suppress it. Entry 21 above records that the plan had already decided this.
The repository's three existing `CA2100` suppressions are unchanged and `.editorconfig` still
holds the rule at `error`.

**Every statement is a constant.** The `CREATE INDEX`, `DROP INDEX`, `sys.indexes` probe,
`CHECKPOINT`, `DBCC DROPCLEANBUFFERS` and the `SET STATISTICS` toggles are all `const string`
fields. The procedure is still invoked through `CommandType.StoredProcedure` with typed
parameters. **Nothing the verb accepts reaches SQL**: `--readings` is parsed to an `int`,
`--output` is a filesystem path, `--skip-single-client` is a flag, and the single-client
identifier is read from the database rather than supplied.

**The destructive operations are reachable only from the command line.** `RollupMeasurer` and
`MeasurementSession` are registered in DI but no endpoint resolves them, and the `measure` verb
runs instead of the web host rather than alongside it. Dropping an index and clearing a server's
buffer pool are developer operations; an HTTP request must not be able to ask for either.

**Two things accepted rather than fixed**, both recorded so they read as decisions:

- **`DBCC DROPCLEANBUFFERS` affects the whole instance.** It is required for a comparable
  measurement and there is no database-scoped equivalent. Mitigated by saying so — the verb
  warns before its first reading and `docs/performance.md` repeats it — rather than by pretending
  the scope is narrower than it is.
- **The measurement briefly leaves the schema without an index.** Unavoidable: that state is
  what is being measured. Mitigated three ways rather than one — restore in a `finally`, ensure
  on entry so a crashed run heals instead of mislabelling the next one, and `state` reporting
  presence so the condition is visible at all. The middle defence is the one that matters and
  the one easiest to leave out.

---

## Security review — feature 003 (constitution P24)

This feature is almost entirely SQL and it moved the access boundary, so P24 applies twice
over. Reviewed before commit.

**Zero new analyzer suppressions.** That was the design target rather than the outcome:
`research.md` R5 states in advance that a `CA2100` appearing anywhere in this feature would be
a design error to fix rather than a finding to justify, because the report takes three scalar
parameters and its command text does not vary. It held. The repository's three existing
CA2100 suppressions are unchanged and `.editorconfig` still keeps the rule at `error`.

**Parameterisation.** Every value crosses as a typed `SqlParameter` — `@FromDate` and `@ToDate`
as `SqlDbType.Date`, `@ClientId` as `SqlDbType.Int` with `DBNull.Value` when absent.
`CommandType` is `StoredProcedure` and the command text is a `const` procedure name, so there
is no string to concatenate into. The optional client is expressed as a null *argument*, not as
a different command — one procedure, one plan, one thing to review.

**Dates do not become strings.** They cross as `DateTime` at midnight against a `date`
parameter. A string date would have reintroduced culture dependence at the boundary, which is
the same class of problem the procedure works to avoid on its own side by anchoring week
arithmetic on a fixed date rather than on `SET DATEFIRST`.

**The access boundary moved and was re-verified.** `/api/v1/ping` — feature 001's placeholder,
which existed only so the boundary could be shown to accept as well as reject — was deleted
when the rollup endpoint landed. The rollup route carries no `AllowAnonymous`, so it inherits
the fallback-closed policy; `/health` and the API documentation remain the only open routes.
`AuthBoundaryTests` now aims its four cases at the rollup instead, which strengthens them: the
boundary is proven on a route that returns database contents rather than a constant.

**One thing accepted rather than fixed.** The single-client filter is applied after ranking, so
a request for one client still aggregates every client's week and discards most of the result.
That is required by FR-012 — a standing of "1 of 1" is information-free — but it means a caller
can make the server do the full-population work while receiving one client's rows. There is no
authorization model in this repository to leak across (PRD §2.2: no multi-tenancy, no RBAC) and
no load to protect, so it is recorded as a known shape rather than treated as a finding. It is
also the path where the next feature's index measurement should be most interesting.

---

## Security review — feature 002 (constitution P24)

Two suppressions and one credential path, reviewed before commit.

- **CA5394 in `SeedDataGenerator`** — insecure randomness. Accepted: this generator produces
  demonstration data and never keys, tokens or identifiers, and reproducibility is a stated
  requirement (FR-020). A seeded generator is the only thing that satisfies it.
- **CA2100 in `ProcedureApplier`, `BulkSeeder` and `SeedVerifier`** — non-literal
  `CommandText`, three sites. Accepted and scoped to the exact lines: the procedure applier
  executes the contents of source-controlled `.sql` files applied by a developer against
  their own database; the bulk seeder interpolates a table and column name that are
  compile-time literals at every call site; the verifier's queries are literals passed
  through a private helper. None takes user input and none crosses a trust boundary.
  `.editorconfig` keeps CA2100 at `error` repository-wide, so any other non-literal SQL
  still fails the build.
- **Development token minting.** Reviewed: it signs with the configured key and the
  algorithm constant, so the printed token cannot drift from what the validator accepts. Its
  claim set is a single identity claim — nothing implying an authorisation model that does
  not exist. It is written to stdout only, never to a file in the repository, and the
  fail-closed behaviour verified in feature 001 still holds, so it is unusable outside
  Development.

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

---

## Security review — feature 006 (constitution P24)

Reviewed before implementation completion:

- All ten new routes inherit the existing fallback authentication policy; none uses
  `AllowAnonymous`.
- Client, matter and timekeeper reads and writes use EF Core expression trees and typed
  values. No feature-006 SQL was assembled from request data, and no CA2100 suppression was
  added.
- Client and matter identifiers come from route parameters. The revise commands deliberately
  carry no client code, matter number, client identifier or timekeeper fields, so immutable
  references cannot be smuggled through a body.
- No POST or PUT route exists for timekeepers. The integration test asserts the absent surface
  returns 405 rather than relying on a handler-side refusal.
- Uniqueness translation matches only SQL Server errors 2601/2627 and the two known index
  names. Unmatched database failures are rethrown; matching by localized message text was
  avoided.

**One implementation defect caught.** The first create path projected the in-memory timestamp
before SQL Server applied the configured `datetime2(3)` precision. The create/read equality test
caught the mismatch; stores now reload the inserted entity before returning it. A test assertion
also initially used the wrong casing for an existing domain message and was corrected after
reading the actual rule contract.

---

## Feature 007 — the test command invented a switch

**What the agent generated:** task T023 and the first dashboard-host test run used
`dotnet test --no-incremental`, extrapolating the repository's required build switch onto the
test command.

**Symptom:** the test did not start. MSBuild exited with `MSB1001: Unknown switch` and showed
`--no-incremental` as the offending argument.

**How it was caught:** by executing the test task before implementation, rather than treating
the generated command as plausible documentation. The gate is now a fresh
`dotnet build --warnaserror --no-incremental` followed by `dotnet test --no-build`; this earns
the non-incremental build evidence and makes the tests consume exactly that assembly.

---

## Security review — feature 007 (constitution P24)

Reviewed the browser token path, anonymous static hosting and the auth-adjacent setup-script
wording before implementation completion:

- The pasted development token is held under one `sessionStorage` key, sent only in the
  `Authorization` header of a same-origin request, never added to a URL, and removed on 401.
  There is no refresh logic, identity endpoint, email/password flow or signing key in the UI.
- `MapDashboardFiles()` serves only the committed export before authorization. The existing
  fallback policy still closes `/api/v1/*`, and `DashboardHostTests` proves `/` returns 200
  while the rollup returns 401 without a token.
- Searches of both `web/` and the generated `wwwroot/` found no JWT value, signing key or
  literal bearer credential. The JWT-shaped text in the input is only a placeholder.
- React renders report strings as text and the dashboard has no raw-HTML sink. The accepted
  risk is the normal SPA one: same-origin script can read `sessionStorage`. That is bounded
  here by the local reviewer scope and the absence of an identified injection path.
- The `Initialize-LocalDb.ps1` change alters only the destination named beside the already
  printed token; token creation and output are unchanged.

No medium-or-higher finding required remediation.

---

## Feature 007 — validation friction

Two validation details were caught by running the generated workflow rather than trusting it:

- A fresh build initially failed with locked API assemblies because the browser-validation
  host was still running. Stopping that host before the gate produced a clean
  `--warnaserror --no-incremental` build and all 128 tests passed from those assemblies.
- Automated browser review covered the anonymous token prompt, accessibility tree and
  768-pixel no-overflow layout, but credential-entry safeguards correctly refused to paste a
  bearer token. The authenticated row comparison, client-filter interaction and the remaining
  keyboard walkthrough are therefore explicit human validation steps rather than claims made
  from source inspection. API-side checks did confirm 495 seeded rows, an empty future range,
  a null first-week delta, a later numeric delta, and the unchanged 401 boundary.

---

## Security review — feature 008 (constitution P24)

`web/app/token-session.ts`, `src/LexTime.Api/Dashboard/DashboardFiles.cs`, JWT
validation, connection strings, and SQL were not modified. Time entries reuses the
007 paste-and-`sessionStorage` session and the existing five time-entry routes.

A search of `web/` and the regenerated `src/LexTime.Api/wwwroot/` found no JWT
value, signing key, or literal bearer credential. Writes send JSON bodies without
a rate field and without `userId` on revise.

No medium-or-higher finding required remediation; there was no new auth or SQL
surface to accept.

---

## Feature 008 — implementation notes

Caught during implementation rather than invented afterwards:

- Recording "today" while the listing is open on the last seed week (2026-08-10
  through 2026-08-13) would hide the new row. After a successful record the
  listing range expands to include the saved work date so SC-002 can be shown
  without a silent filter miss.
- The Time entries host test is a pin, not a failing-first test: the collection
  was already 401 without a token. It exists so serving HTML cannot quietly open
  the seed.
- `dotnet test --no-incremental` remains invalid (feature 007). The gate used
  here was `dotnet build --warnaserror --no-incremental` then
  `dotnet test --no-build` (129 tests).
- Against the running host: unauthenticated `GET /` is 200 HTML;
  `GET /api/v1/time-entries` is 401. The seed week
  `2026-08-10`–`2026-08-13` lists `total=2933` with a 20-row page whose first
  row is work date 2026-08-10, 12 minutes, captured rate 185. The empty future
  window returns `total=0`. A 7-minute POST returns 400 with every
  `violations[]` entry (duration increment plus, for matter 1, an inactive
  client) rather than only the first. Authenticated browser walkthroughs still
  need a human to paste the development token; the 007 credential-entry
  safeguard applies here too.

---

## Security review — feature 009 (constitution P24)

`web/app/token-session.ts`, `src/LexTime.Api/Dashboard/DashboardFiles.cs`, JWT
validation, connection strings, and SQL were not modified. Party directories
reuse the 007 paste-and-`sessionStorage` session and the existing 006 routes.

A search of `web/` found no JWT value, signing key, or literal bearer
credential. Register and correct send JSON bodies without a client code or
matter number on update, without a timekeeper write helper, and without a
firm-wide matters request.

No medium-or-higher finding required remediation; there was no new auth or SQL
surface to accept.

---

## Feature 009 — implementation notes

Caught during implementation rather than invented afterwards:

- The client and timekeeper host tests are pins, not failing-first tests: both
  collections were already 401 without a token. They exist so serving HTML
  cannot quietly open the directories.
- `web/app/party-lookups.ts` was left on `take=200` for Time entries pickers.
  Directory screens use `parties-api.ts` with `skip`/`take` of 20, 50, or 100
  so a paged Clients view cannot silently shrink the 008 matter picker.
- Matters have no sidebar destination. Nesting them on the selected client is
  the P3 save that kept Timekeepers in this spec.
- Against the running host: unauthenticated `GET /` is 200 HTML;
  `GET /api/v1/clients` and `GET /api/v1/users` are 401, same as time entries.
  Authenticated listing, uniqueness collisions, and deactivation walkthroughs
  still need a human to paste the development token; minting one from this
  session was refused as credential material, matching the 007 safeguard.
