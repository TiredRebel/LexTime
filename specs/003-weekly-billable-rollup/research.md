# Research — Weekly Billable Rollup (003)

Phase 0 output. Each item is a decision, why it was taken, and what was rejected. Where a
claim about SQL Server behaviour is made, it was **run** against the SQL Server 2022 container
rather than recalled — the output is quoted. Constitution P8 governs performance claims; the
same standard is applied here to behavioural ones, because a design resting on a
misremembered date function is the same class of error.

---

## R1. Weeks are identified by a day-count ordinal, not by week number

**Decision.** Every week is identified internally by
`WeekIndex = DATEDIFF(day, '19000101', WorkDate) / 7`, and everything reported about the week
is derived from that ordinal:

| Reported field | Derivation |
| --- | --- |
| `WeekStartDate` | `DATEADD(day, WeekIndex * 7, '19000101')` |
| `IsoYear` | `YEAR(DATEADD(day, 3, WeekStartDate))` — the year containing that week's Thursday |
| `IsoWeek` | `DATEPART(ISO_WEEK, WeekStartDate)` |

**Rationale.** Three separate properties are needed and only the ordinal has all three.

*It must anchor on Monday without depending on session state.* `DATEPART(weekday, …)` shifts
with `SET DATEFIRST` and `SET LANGUAGE`, so a procedure built on it returns different answers
to different callers. The anchor date removes the dependency — and it is the right anchor:

```
AnchorIsMonday
--------------
Monday
```

*ISO week numbering must not shift either.* Verified directly, running the same expression
under both extremes of `DATEFIRST`:

```
Df IsoWk_20260101 IsoWk_20251229
-- -------------- --------------
 7              1              1
 1              1              1
```

`DATEPART(ISO_WEEK, …)` is genuinely ISO and ignores `DATEFIRST`. Good — but note this only
covers the week *number*. SQL Server has no ISO-year part at all, which is why the Thursday
rule is computed by hand above.

*It must be contiguous across a year boundary.* This is the property FR-008 depends on and the
reason the ordinal exists:

```
d          Monday     WeekIndex IsoYear IsoWeek
---------- ---------- --------- ------- -------
2025-12-29 2025-12-29      6574    2026       1
2026-01-01 2025-12-29      6574    2026       1
2026-01-04 2025-12-29      6574    2026       1
2026-01-05 2026-01-05      6575    2026       2
2024-12-30 2024-12-30      6522    2025       1
```

Read the January rows carefully, because they are the whole point. The week beginning Monday
2025-12-29 runs into January and is correctly attributed to **ISO year 2026, week 1** — a week
whose dates are mostly in 2025. The week before it is ordinal 6573, which is 2025 week 52.
`WeekIndex - 1` finds it. `IsoWeek - 1` would compute `1 - 1 = 0`, which is not a week, and
`IsoYear` would need decrementing as well — and only in the years where the previous year had
52 weeks rather than 53.

**Alternatives rejected.**

- *`(IsoYear * 100) + IsoWeek` as the ordinal.* The obvious encoding, and wrong every January:
  it makes 2026 week 1 follow 2025 week 52 with a gap of 49 in the ordinal. It would look
  correct in every test that did not cross a year boundary, which is precisely why FR-023
  exists.
- *A calendar dimension table.* Correct, conventional, and a whole table plus its population
  and its migration for something three expressions produce. PRD §2.2's reasoning against
  scope applies.
- *Computing weeks in C#.* Violates FR-014 and P5.

---

## R2. The week-on-week change combines `LAG()` with a contiguity check

**Decision.**

```sql
HoursDeltaVsPriorWeek = CASE
    WHEN WeekIndex - 1 < @FirstWeekIndex        THEN NULL          -- before the range
    WHEN PrevWeekIndex = WeekIndex - 1          THEN BillableHours - PrevBillableHours
    ELSE                                             BillableHours -- silent week = zero
END
```

where `PrevWeekIndex` and `PrevBillableHours` come from `LAG(…) OVER (PARTITION BY ClientId
ORDER BY WeekIndex)` and `@FirstWeekIndex` is the ordinal of the requested start date.

**Rationale.** FR-002 keeps rows sparse and FR-008 wants the *calendar* prior week; a plain
`LAG` gives the prior **row**, which is a different thing the moment a client goes quiet. The
contiguity check is what reconciles them: `LAG` supplies the candidate, and comparing
`PrevWeekIndex` against `WeekIndex - 1` decides whether that candidate is actually last week
or something older. When it is older, the client billed nothing last week, and the change is
the whole of this week's hours.

The three branches map exactly onto the three cases the spec distinguishes, in the order they
must be tested:

1. There is no last week *visible* — the range starts here. Absent, not zero (FR-008, second
   paragraph). Checked first because a row can be both the client's first row and at the range
   edge, and the range edge is the stronger claim.
2. Last week is present in the result. Ordinary subtraction.
3. Last week is inside the range but the client has no row in it. It billed nothing.

Note what case 1 does *not* say: it is not "this is the client's first row." A client whose
first activity is in week 5 of a 10-week range has weeks 1–4 visible and silent, so its week 5
change is its full hours, not absent. The condition is about the range, not about the client.

**Verified viable** against the seeded database over `2026-01-05` to `2026-02-01`. The first
reported week returns `Delta = NULL` for every client, which is case 1 firing correctly: the
range starts on a Monday, so `WeekIndex - 1` falls outside it.

```
IsoYear IsoWeek WeekStart  ClientCode BillHrs  Amount     CumHrs  Delta  Rank
------- ------- ---------- ---------- -------  ---------  ------  -----  ----
   2026       2 2026-01-05 CL001       647.70  252178.50  647.70   NULL     1
   2026       2 2026-01-05 CL002       311.70  119482.00  311.70   NULL     2
   2026       2 2026-01-05 CL003       212.60   85845.50  212.60   NULL     3
```

**This run is not evidence the procedure is correct**, and must not be treated as such.
Constitution P15 forbids accepting generated SQL on the strength of a green run, and the
figures above are self-consistent by construction. It establishes only that the approach
executes and produces the right *shape*. Correctness is FR-021's hand-computed fixture, and
nothing else in this feature discharges it.

**Alternatives rejected.**

- *Materialise a row per client per week.* Makes `LAG` trivially correct and costs everything
  else: 6,240 rows regardless of activity, and `DENSE_RANK` collapsing into a mass tie at zero
  every week. Rejected in the spec's clarification session, recorded here as the reason.
- *`LAG(…, 1, 0)` with a default of zero.* Silently converts case 1 into case 3 — every
  client's first row would report its whole hours as a change from a week nobody looked at.
- *Two passes, second one in C#.* FR-014.

---

## R3. The procedure file is one batch, with nothing before `CREATE OR ALTER`

**Decision.** `db/programmability/usp_WeeklyBillableRollup.sql` contains exactly one
statement: the `CREATE OR ALTER PROCEDURE`. No `GO`, no `SET ANSI_NULLS`/`SET
QUOTED_IDENTIFIER` preamble, no `IF OBJECT_ID(...) DROP`. `SET NOCOUNT ON` goes *inside* the
procedure body, where it is legal.

**Rationale.** This is a constraint the existing applier imposes, and finding it at
implementation time would be an avoidable failure. `ProcedureApplier.ApplyAllAsync` reads each
file and executes it as a single `SqlCommand`
([ProcedureApplier.cs:70](../../src/LexTime.Infrastructure/Maintenance/ProcedureApplier.cs)).
`SqlCommand` has no batch parser, so `GO` — which is a client-tool directive, not T-SQL —
would arrive at the server as a syntax error. And `CREATE OR ALTER PROCEDURE` must be the
first statement in its batch, so any preamble in the same file breaks it just as surely.

Neither constraint is a defect in the applier: one file, one object, one batch is exactly what
P7 asks for, and it is what makes the file's history a readable diff.

---

## R4. Layering: an interface, a handler, and no `ToDto()`

**Decision.**

```
Api            ReportEndpoints.MapReportEndpoints()      validates input, maps result to status
  ↓
Application    GetWeeklyBillableRollupHandler            the one use case (P4)
               IWeeklyBillableRollupReader               declared here, implemented elsewhere
               WeeklyBillableRollupQuery / Row / Response
  ↓
Infrastructure SqlWeeklyBillableRollupReader             SqlCommand + SqlDataReader (P5)
```

**Rationale.** This is the shape P4 and P5 jointly require, and this feature is the first to
exercise it — `LexTime.Application` has held nothing but its registration method since feature
001, which documented it as waiting for exactly this interface.

**No `ToDto()` extension method, and that is not a P4 violation.** P4 mandates `ToDto()` for
*entity*-to-DTO translation. The rollup row is never an entity: it does not exist in the domain
model, has no key, is never tracked, and is materialised straight from a reader into the record
that is serialised. Inserting a hand-written mapping step between two identical shapes would
add a layer that only copies fields — which is the same objection P4 itself raises against a
generic repository. The reader returns `IReadOnlyList<WeeklyBillableRollupRow>` and the handler
wraps it in the response envelope.

**Alternatives rejected.**

- *A separate Infrastructure-side read model mapped to an Application DTO.* Two records with
  the same eleven fields and a mapper between them, to no end.
- *Returning `DataTable` or `dynamic` from the reader.* Loses the compile-time check that is
  half the reason P4 refuses AutoMapper.
- *Endpoint calling the reader directly, skipping the handler.* Would work and would violate
  P4's "every use case is one handler class". The handler also has real work: it owns the
  envelope and echoes the requested range back.

---

## R5. Every parameter is a `SqlParameter`; nothing is concatenated

**Decision.** `@FromDate`, `@ToDate` and `@ClientId` are added as typed `SqlParameter`s with
explicit `SqlDbType`. The optional client is passed as `DBNull.Value` when absent, never by
building a different command text. `CommandType` is `StoredProcedure`, so the command text is
a constant procedure name.

**Rationale.** P24 requires a security review of anything touching SQL, and the cheapest
review is one with nothing to find. This is also what keeps CA2100 quiet without a
suppression: `.editorconfig` holds CA2100 at `error` repository-wide, and the three existing
suppressions are all in code that genuinely cannot be parameterised (a file's contents, a bulk
copy's table name). This feature adds no fourth. **A CA2100 suppression appearing in this
feature should be treated as a design error, not as something to justify** — the report takes
three scalar parameters and there is no reason for its command text to vary at all.

Dates cross as `SqlDbType.Date` from `DateOnly`, not as strings. String dates would reintroduce
culture dependence at the boundary, which R1 just finished removing inside the procedure.

---

## R6. The optional client parameter uses the catch-all pattern, and the trade-off is stated

**Decision.** `WHERE (@ClientId IS NULL OR ClientId = @ClientId)`, applied in the outer select
after the window functions. No `OPTION (RECOMPILE)`, no dynamic SQL, no second procedure.

**Rationale.** The filter must come after ranking, because FR-012 requires the standing to be
the client's position among *all* clients that week rather than one out of one. That fixes the
placement. What remains is the well-known cost: one cached plan serves both the all-clients and
the single-client call, and whichever shape compiles first is imposed on the other.

`OPTION (RECOMPILE)` is the standard answer and is deliberately **not** taken here, for a
reason specific to this repository: the next feature measures this procedure before and after
an index, and `RECOMPILE` would make every execution's plan depend on the parameter values
supplied, turning a before/after comparison into a comparison of two differently-compiled
plans. P8 says the measurement drives the claim; pre-emptively tuning against an unmeasured
problem inverts that. The pattern is named in a comment in the procedure so a reviewer sees it
was chosen, and the next feature can add `RECOMPILE` with numbers justifying it.

**Worth flagging for that feature**: because standing is ranked before the filter, a
single-client request still aggregates every client's week and discards most of the result.
That path, not the full-range one, is where the missing index should hurt most.

**Alternatives rejected.**

- *Two procedures.* Duplicates ninety lines of window functions to avoid one predicate.
- *Dynamic SQL.* Reintroduces exactly the CA2100 surface R5 avoided, for a demo-scale query.

---

## R7. Keeping the hand-computed fixture honest

**Decision.** The fixture is a small dataset — a handful of clients over a few weeks — built
by explicit inserts, with every expected figure written as a literal in the test. The tests
call **the procedure directly**, not the endpoint (SC-009).

The discipline that makes this real, rather than a ritual: the expectations are derived from
the fixture's inputs on paper and committed *before* the procedure is run against it. Where the
procedure and the expectation disagree, the default assumption is that the procedure is wrong.

**Rationale.** P12 exists because window-function bugs are self-consistent — a wrong running
total is wrong in the same direction on every row, so nothing looks anomalous. The only thing
that catches it is an expectation computed by a different method. P15 adds that this is exactly
the category of agent output that must never be accepted on a green run.

**The fixture must make the two readings of FR-008 disagree.** If the gap case is built so
that "this week's own hours" and "the difference against the week the client last billed in"
happen to produce the same number, the test passes under both the correct implementation and
the wrong one, and FR-022 is not satisfied in substance. Concretely: the week before the gap
must have a **different** billable total from the returning week, and neither may be zero.

Calling the procedure rather than the endpoint also localises failure. A test that goes through
HTTP is asserting the procedure, the reader, the handler, the endpoint, the serialiser and the
route all at once; when it goes red it names none of them.

---

## R8. The test fixture must apply procedures, not only migrations

**Decision.** `SqlServerFixture` gains a procedure-application step after `MigrateAsync()`, on
both the shared database and `CreateIsolatedDatabaseAsync`. It locates the repository root by
walking up from `AppContext.BaseDirectory` looking for `LexTime.sln`, duplicating the six-line
walk that `MaintenanceCommands.FindRepositoryRoot` already does privately.

**Rationale.** The fixture currently applies migrations only
([SqlServerFixture.cs:38](../../tests/LexTime.IntegrationTests/SqlServerFixture.cs)), which
was sufficient while `db/programmability/` was empty. From this feature it is not: every rollup
test would fail with "could not find stored procedure", and the failure would point at the test
setup rather than at anything real.

Duplicating the root walk is deliberate rather than promoting it to a shared public helper.
It is six lines with no state, the test project already needs the same answer for its own
reasons, and widening `LexTime.Api`'s public surface to share it would be a worse trade than
the duplication. If a third caller appears, that is the point to extract it.

---

## R9. Two existing test helpers need widening

**Decision.**

- `DirectSql.InsertTimeEntryAsync` takes `isBillable` and `hourlyRate` as parameters. Both are
  currently hard-coded to `1` and `350.00`
  ([DirectSql.cs:107](../../tests/LexTime.IntegrationTests/DirectSql.cs)), which cannot express
  a non-billable entry or two clients at different rates — and FR-022 requires both.
- `AuthBoundaryTests` retargets from `/api/v1/ping` to the rollup endpoint, and the `ping`
  placeholder is deleted from `Program.cs`.

**Rationale on the second.** `Program.cs` says of `ping`: *"Removed when the first real
endpoint lands"* ([Program.cs:92](../../src/LexTime.Api/Program.cs)). This is that endpoint.
Leaving both would leave an unauthenticated-by-accident-looking route in a repository whose
access boundary is one of the things a reviewer checks. The four boundary tests keep their
assertions and change only the route they aim at — which strengthens them, because they then
test the boundary on a route that actually returns data.

Note the consequence: the accepted-token test must now supply `from` and `to`, and its
database must have the procedure applied. R8 covers the second; the first is a two-line change
to the request URI.

---

## R10. Arithmetic is done in one place and rounded once

**Decision.** Minutes are summed as integers, cast once to `decimal(18,4)`, and divided by
`60.0` to give hours. The amount is `SUM(DurationMinutes * HourlyRateSnapshot) / 60.0` — the
multiplication inside the sum, the division outside it — cast to `decimal(14,2)` only at the
output. Hours are returned as `decimal(12,2)`.

**Rationale.** Rounding per row and then summing would drift by up to half a cent per entry
across 400,000 entries. Summing in the integer domain first and converting once cannot. The
output precision is sufficient and exact: durations are multiples of six minutes, so hours are
always multiples of `0.1`, and SQL Server's `decimal` is exact rather than binary
floating-point.

`decimal`, never `float`. A money column reported through a binary float is the kind of detail
that costs more credibility than it saves effort.

---

## R11. The reader gets its connection string from the registration closure

**Decision.** `AddLexTimeInfrastructure` already resolves the connection string and validates
it. The reader is registered as
`services.AddScoped<IWeeklyBillableRollupReader>(_ => new SqlWeeklyBillableRollupReader(connectionString))`,
capturing the value that method already has in hand.

**Rationale.** The reader must not depend on `LexTimeDbContext` — taking the connection from
EF Core to prove the point that this path does not use EF Core would be self-defeating, and it
would tie the reader's lifetime to the context's. One line and no new type; the existing
`DevelopmentTokenMinter` registration takes the same shape.

**Alternatives rejected.** An `ISqlConnectionFactory` interface with one implementation and one
consumer — an abstraction with nothing on the other side of it.

---

## Open questions

None. Every `NEEDS CLARIFICATION` from the spec was closed in its clarification session before
this plan began, and no new one arose during design.
