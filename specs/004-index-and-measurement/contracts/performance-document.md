# Contract — `docs/performance.md`

The published account. `docs/prd.md` §6.6 makes this a done criterion for the repository, and
constitution P8 governs every figure in it.

This is the document a senior reviewer opens second, after the rollup itself. It is also the
one place where a single invented number would discredit everything else in the repository, so
the contract below is mostly about provenance.

## Required sections

### 1. Method, before any figure

What was measured, on what, and how — stated before the results so a reader can judge the
numbers rather than discover the caveats after believing them.

- The dataset: 400,000 entries, reference date 2026-08-13, random seed 20260813, and the fact
  that all three are committed constants, which is what makes the run repeatable.
- The range measured, and that it is the seed's full window.
- The four combinations and why the single-client shape is measured separately — the report
  ranks every client before narrowing to one, so that path does the full-population work and
  discards most of it.
- The cache convention: `CHECKPOINT` and `DBCC DROPCLEANBUFFERS` before every reading, applied
  identically to both index states, **with the warning that it clears the buffer pool for the
  whole instance** (R5).
- Readings per combination, and how they are reduced: read counts as one figure, elapsed time as
  a median with its range.
- **Why read counts lead and elapsed time follows.** Logical reads are a property of the plan
  and reproduce exactly anywhere. Elapsed time is a property of the machine. A reader who is
  told this will not test the claim against the wrong number.
- The machine and the SQL Server version (FR-021).

### 2. The index

Its definition, and what each part is for — why those two key columns, why those three included
columns, and why `UserId` and `Narrative` are not among them.

### 3. Results

One table, four rows.

| Shape | Index | Logical reads | Elapsed median | Elapsed range | Rows |
| --- | --- | --- | --- | --- | --- |

Every cell traces to a committed file. The table is a summary of the raw output, not a
substitute for it.

### 4. Plan shape

For each shape, what changed between the two plans — or plainly that it did not (FR-017). Name
the operators. If a sort disappeared, say which one; if a scan became a seek, say on what; if a
spill to `tempdb` stopped happening, say so. A reader should be able to open the two `.sqlplan`
files and find what the paragraph describes.

**"No change" is a permitted and complete answer.** If the read counts moved and the plan shape
did not, that is the result, and dressing it up as a structural change would be the failure this
whole document exists to avoid.

### 5. What the numbers do not say

The honest limits, stated by the author rather than discovered by the reader:

- One machine, one run of the protocol, one dataset size.
- Elapsed times are not portable.
- The index costs something on write, and no write-path measurement was taken.
- If the improvement is modest, **why** it is modest — which of the query's costs the index does
  not touch.

### 6. Reproducing it

The commands, in order, from a cold clone. Two to get the environment, one to measure. What the
reader should expect to match exactly (read counts, result hashes) and what will legitimately
differ (elapsed times).

## Provenance rules

These bind every figure in the document and in the README section it feeds.

1. **Every number comes from a committed raw file.** No figure appears in the summary that
   cannot be found in `docs/performance/statistics-*.txt`.
2. **Nothing is rounded, adjusted, or "cleaned up".** If a reading is odd, the oddity is
   published and explained.
3. **No figure from another machine, another dataset size, or another run of the procedure**
   than the one described in the method.
4. **No target, no expectation, no service level.** This document reports what is, not what
   should be. A number nobody measured against is exactly the kind of claim P8 exists to
   prevent.
5. **No placeholder survives.** The four `TODO(measure)` markers currently in the README are
   replaced with captured figures, and none remains anywhere in the repository (FR-019).
6. **If the result is disappointing, it is published anyway**, with an explanation (FR-018). The
   seed is not enlarged to improve it. A modest, well-explained result reads as judgement; a
   suspiciously good one invites a reviewer to start checking, and they will be right to.

## Companion files

| Path | What it is |
| --- | --- |
| `docs/performance/plan-{shape}-{state}.sqlplan` | Four actual execution plans, with runtime counters, openable |
| `docs/performance/statistics-{shape}-{state}.txt` | Four verbatim `STATISTICS IO`/`TIME` captures |

Both sets are committed. Their existence is what turns the summary table from an assertion into
something a reviewer can check without running anything — and running it is available to them
too.
