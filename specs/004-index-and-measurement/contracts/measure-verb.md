# Contract — `measure` verb

The seventh verb on the host's command-line surface, joining `migrate`, `apply-procedures`,
`seed`, `verify-seed`, `state` and `mint-token` from feature 002. Same dispatcher, same exit
codes, same contract shape: `specs/002-bootstrap-and-seed/contracts/host-cli.md`.

It exists so the measurement needs no tool the quickstart does not already need (FR-015).

## Invocation

```powershell
dotnet run --project src/LexTime.Api measure
```

| Argument | Required | Default | Meaning |
| --- | --- | --- | --- |
| `--readings <n>` | no | `5` | Readings per combination (R6) |
| `--output <dir>` | no | `docs/performance` | Where plans and raw statistics are written |
| `--skip-single-client` | no | off | Measures only the full-range shape. The escape hatch the plan's cut order names |

No connection argument. It reads the same configured connection string as every other verb, so
there is no way to point it at one database while believing it is another.

## What it does, in order

1. **Ensure the index exists.** If it is missing — the state a previous interrupted run leaves
   behind — create it before doing anything else, and say so. Without this a crashed run
   followed by a clean one produces measurements that are quietly mislabelled (R7).
2. **Warn about the buffer pool.** `DBCC DROPCLEANBUFFERS` clears the whole instance, not one
   database. The verb states this before it starts rather than in a footnote.
3. For each combination — two index states × two request shapes:
   - drop or create the index so the state matches
   - for each of N readings: `CHECKPOINT`, `DBCC DROPCLEANBUFFERS`, run with
     `SET STATISTICS IO`/`TIME`, collect the messages
   - once per combination: run again with `SET STATISTICS XML` and capture the actual plan
   - hash the result set
4. **Compare the hashes** across index states for each shape. A mismatch is a failure, not a
   note — it means the index changed the answer (FR-003).
5. **Restore the index** in a `finally`, whatever happened.
6. **Write** the plans and raw statistics to the output directory, and print the summary table.

## Output

On success, to stdout: a table of the four combinations with logical reads, median elapsed time
and range, the row count, and the equivalence verdict. Then the paths written.

The table is for a human reading the terminal. `docs/performance.md` is the published artefact
and is written by hand from these files — see
[performance-document.md](./performance-document.md).

To the output directory:

| File | Content |
| --- | --- |
| `plan-{shape}-{state}.sqlplan` | Actual execution plan, four of them, openable in SSMS or Azure Data Studio |
| `statistics-{shape}-{state}.txt` | Verbatim `STATISTICS IO`/`TIME` output, unedited |

**Verbatim means verbatim.** The raw files are what make the summary table auditable rather
than merely believable, so the verb writes the message text exactly as the server sent it —
no reformatting, no rounding, no trimming to the interesting lines (R3).

## Exit codes

Reuses `ExitCodes` from feature 002.

| Code | When |
| --- | --- |
| `Success` | Every combination measured, hashes matched, index restored |
| `ConfigurationError` | Connection string missing or unusable |
| `OperationFailed` | The database is unreachable, or a statement failed |
| `VerificationFailed` | **The two index states returned different results.** The one failure this verb exists to be able to report |

A non-zero exit still leaves the index restored. Failing and degrading the schema at the same
time is the outcome R7 is written to prevent.

## Guarantees

- **The database ends indexed.** Every path, including exceptions and cancellation (FR-014).
- **Repeatable.** Two runs against the same seeded database produce identical logical read
  counts and identical result hashes (FR-016, SC-004). Elapsed times will differ; that is why
  they are reported as a median and a range rather than as a figure.
- **No writes to application data.** It reads the report and toggles one index. No entry,
  client, matter or user is touched.
- **No new dependency.** Nothing installed, nothing to configure, no account.

## Not in this contract

- **It does not write `docs/performance.md`.** The published account contains a paragraph of
  judgement about what changed in the plan's shape, and generating that automatically would
  make it a template rather than an observation.
- **It does not fail on a small improvement.** FR-018: the measurement reports what is true. A
  verb that treated an unimpressive result as an error would be an instruction to keep running
  it until it said something better.
- **It is not a benchmark harness.** Four combinations, one query, one machine. `docs/prd.md`
  §2.2 rules out anything larger.
