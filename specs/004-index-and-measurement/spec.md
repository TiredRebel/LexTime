# Feature Specification: Index and Measured Performance

**Feature Branch**: `004-index-and-measurement`

**Created**: 2026-08-14

**Status**: Draft

**Input**: The performance half of the split made at feature 003, which held the covering
index back so this feature would have an honest "before" to measure. Covers the index itself,
the captured before-and-after evidence, and the published account of what changed. Scope
follows `docs/prd.md` §3 Indexes and §6 done criterion 6, under constitution P8 — the
principle that a performance claim is only worth what its measurement is.

## Clarifications

### Session 2026-08-14

- Q: If the covering index turns out to make little difference at 400,000 entries, should the
  seed volume be raised and the measurement retaken? → A: No. The seed stays at its committed
  volumes and whatever the measurement says is what gets published, with an honest explanation
  of why the delta is the size it is. Constitution P8 instructs exactly this, and `docs/prd.md`
  §8's contrary mitigation was written before feature 002 committed to a reproducible dataset:
  raising the volume now would invalidate that feature's row-count criterion, its verification
  bands and every test asserting those figures, in order to make one number look better. A
  modest, well-explained result reads as judgement; a suspiciously good one invites a reviewer
  to start checking.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The report gets faster and returns exactly what it did before (Priority: P1)

The covering index is added to the schema. A reviewer with a seeded database runs the rollup
and gets the same figures, to the last decimal place, as they got before the index existed —
only sooner.

**Why this priority**: An index that changes results is worse than no index at all, and it is
the failure that hides best: the numbers are still plausible, still self-consistent, and
nobody looks. Nothing else in this feature is worth doing until that is ruled out.

**Independent Test**: Run the rollup over the full seeded range with the index absent, capture
every row, add the index, run it again, and compare the two result sets row for row. Delivers
value alone: a faster report whose correctness is demonstrably untouched.

**Acceptance Scenarios**:

1. **Given** a seeded database without the index, **When** the rollup runs over the full
   seeded range and the index is then added and it runs again, **Then** both result sets are
   identical row for row and field for field, including row order.
2. **Given** the same comparison narrowed to a single client, **When** both runs complete,
   **Then** the two result sets are identical.
3. **Given** a fresh clone, **When** the schema is applied, **Then** the index is present
   without any manual step.
4. **Given** the index is present, **When** the rollup's correctness tests run, **Then** every
   one still passes unchanged.

---

### User Story 2 - A reviewer can regenerate the numbers rather than trust them (Priority: P2)

Someone evaluating this repository does not have to take the published figures on faith. A
documented procedure on their own machine reproduces both halves of the comparison — the
un-indexed state and the indexed one — and yields the same read counts.

**Why this priority**: Constitution P8 and P23 make the same argument from two directions: a
result the reviewer cannot regenerate is an assertion, not evidence. This feature is the one
place in the repository where that is most easily faked and most valuable when it is not.

**Independent Test**: Follow the documented procedure end to end on a machine that has only
the two quickstart commands behind it, and compare the read counts obtained against the
published ones.

**Acceptance Scenarios**:

1. **Given** a seeded database, **When** the documented measurement procedure is run, **Then**
   it produces read counts for both index states without any manual database surgery.
2. **Given** the procedure has been run, **When** the resulting read counts are compared with
   the published ones, **Then** they match exactly.
3. **Given** the procedure has been run twice, **When** its read counts are compared between
   runs, **Then** they are identical.
4. **Given** the procedure has completed, **When** the database is inspected, **Then** it is
   left in the indexed state — the measurement does not leave the schema degraded.

---

### User Story 3 - The single-client path is measured on its own (Priority: P3)

The report ranks every client before narrowing to one, so a single-client request does the
full-population work and discards most of it. That path is measured separately rather than
being assumed to behave like the full-range one.

**Why this priority**: It was identified during feature 003 as the case where the missing
index should cost most, and a performance section that measured only the obvious query would
be reporting the easy half. It is P3 because the full-range measurement is what the done
criterion names.

**Independent Test**: Measure the single-client request in both index states and compare its
change against the full-range request's.

**Acceptance Scenarios**:

1. **Given** both index states, **When** the single-client request is measured in each,
   **Then** its read counts are captured and published alongside the full-range ones.
2. **Given** both sets of figures, **When** they are compared, **Then** the published account
   states whether the two paths benefit differently and by how much.

---

### Edge Cases

- **The measured improvement is small or absent.** Covered by FR-018 — this is the case the
  feature must handle honestly rather than the case it must avoid.
- **Elapsed times differ between machines.** Expected. Read counts are cache-independent and
  reproduce exactly; wall-clock time does not, and the published figures must not imply
  otherwise.
- **The first run of a query is slower than the rest** because nothing is cached yet. The
  protocol must state how it handles this rather than letting whichever run happened first
  become the published number.
- **The database is left without the index** after an interrupted measurement. The schema must
  end in its committed state whatever happens.
- **A reviewer runs the procedure against a database seeded with different volumes.** The
  published figures are only comparable against the committed seed; the procedure must say so
  rather than silently producing numbers that look comparable and are not.
- **The plan shape does not change**, only the read counts. That is a legitimate outcome and
  the account must be able to describe it.

## Requirements *(mandatory)*

### Functional Requirements

**The index**

- **FR-001**: The schema MUST gain a non-clustered index on the time entry table keyed on
  billing date and the billable flag, carrying the matter reference, the duration and the rate
  snapshot as included columns — exactly the definition `docs/prd.md` §3 commits to.
- **FR-002**: The index MUST be part of the schema a fresh clone receives, applied by the same
  step that applies the rest of the schema. No manual statement, no extra command.
- **FR-003**: Adding the index MUST NOT change any figure the report returns. The report's
  result set MUST be identical before and after, row for row, field for field, and in the same
  order.
- **FR-004**: The rollup's existing correctness tests MUST pass unchanged with the index
  present. Any change to an expected value in those tests is a defect in this feature, not an
  update.

**The measurement**

- **FR-005**: Both index states MUST be measured: without the index and with it.
- **FR-006**: Two request shapes MUST be measured in each state — the full seeded range across
  all clients, and the same range narrowed to a single client — giving four measured
  combinations.
- **FR-007**: For every combination the following MUST be captured from a real run: the
  logical read count, the elapsed time, and the execution plan.
- **FR-008**: Execution plans MUST be committed in a form a reviewer can open and inspect, not
  described in prose alone and not as an image.
- **FR-009**: Every measurement MUST be taken against the committed seed at its committed
  volumes and reference date. A measurement taken against a differently-sized dataset is not
  comparable and MUST NOT be published as if it were.
- **FR-010**: The measurement procedure MUST control for caching rather than ignore it, and
  MUST state what it does. Whichever convention is chosen MUST be applied identically to both
  index states, or the comparison measures the cache rather than the index.
- **FR-011**: Elapsed time MUST be reported from more than one run, in a form that shows the
  spread rather than a single figure that happened to be observed once.
- **FR-012**: Read counts MUST be reported as the primary evidence and elapsed time as
  secondary, with the reason stated: read counts are a property of the plan and reproduce
  exactly, while elapsed time is a property of the machine and does not.

**Reproducibility**

- **FR-013**: A reviewer MUST be able to regenerate both halves of the comparison by a
  documented procedure, without hand-editing the schema.
- **FR-014**: That procedure MUST leave the database in its committed state — indexed — however
  it ends, including when it fails partway.
- **FR-015**: The procedure MUST NOT require any tool beyond what the quickstart already
  requires. A measurement only reproducible with a database IDE installed is not reproducible
  by the audience this repository is written for.
- **FR-016**: Running the procedure twice MUST produce identical read counts.

**The published account**

- **FR-017**: A performance document MUST publish, for each of the four combinations, the read
  counts, the elapsed times, the plan, and a paragraph naming what changed in the plan's shape
  — or stating plainly that it did not change.
- **FR-018**: If the measured improvement is small, the small number MUST be published together
  with an explanation of why it is small. The seed volume MUST NOT be raised in response to a
  disappointing result, and no figure in this feature may come from a dataset other than the
  committed one. The measurement reports what is true of the repository as it ships.
- **FR-019**: Every performance placeholder currently standing in the README MUST be replaced
  with a captured figure. No placeholder may remain anywhere in the repository once this
  feature is done.
- **FR-020**: No figure published by this feature may be estimated, rounded from memory,
  illustrative, or carried over from another machine's run. Every number MUST trace to a run
  whose procedure is documented (constitution P8).
- **FR-021**: The document MUST state the machine and database version the figures were taken
  on, so a reviewer whose numbers differ knows whether that is expected.

### Key Entities

- **Index state**: one of two conditions the database is measured in — without the covering
  index, and with it. Everything else about the database is held identical between them.
- **Measured combination**: an index state paired with a request shape. Four exist. Each
  carries a read count, a set of elapsed times, and a plan.
- **Reading**: one captured run of one combination. Multiple readings per combination are what
  make the elapsed-time spread visible.
- **Seed dataset** *(existing)*: the committed 400,000-entry dataset at its fixed reference
  date. Its determinism is the reason two runs are comparable at all, and every published
  figure is relative to it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The report's output over the full seeded range is identical with and without the
  index — zero differing rows across all 24 months, and zero differing fields.
- **SC-002**: All 58 existing tests pass with the index present, with no expected value edited.
- **SC-003**: A reviewer running the documented procedure obtains read counts that match the
  published ones exactly, for all four combinations.
- **SC-004**: Two runs of the procedure on the same machine produce identical read counts.
- **SC-005**: The procedure completes without the reviewer typing a database statement, and
  leaves the database indexed.
- **SC-006**: Zero performance placeholders remain in the repository; the four in the README
  are replaced with captured figures.
- **SC-007**: Every published figure is traceable to a documented run — an independent reader
  can name, for each number, which run produced it.
- **SC-008**: The published account states the plan-shape change, or states that there was
  none, for each of the four combinations.

## Assumptions

- **The index belongs in the schema, not in a script.** It is a table structure, and
  constitution P7 keeps only *procedures* out of migrations. A reviewer who applies the schema
  gets the index; the un-indexed state is something the measurement procedure creates
  deliberately and temporarily.
- **The un-indexed state is reached by removing the index, not by withholding it.** The
  alternative — shipping the schema un-indexed and adding the index by a separate step — would
  leave every fresh clone slower than the repository claims it is, to serve a measurement taken
  once.
- **Read counts are the claim; elapsed time is the illustration.** Logical reads are a property
  of the plan and identical on every machine. Elapsed time depends on hardware, other load, and
  what the buffer pool happened to hold. Publishing them with equal weight would invite a
  reviewer to find that one does not reproduce and discard both.
- **The procedure runs against the existing local environment**, the one the quickstart brings
  up. It is not a benchmark harness, and PRD §2.2 rules one out.
- **Plans are committed as files rather than screenshots**, so they can be opened and read
  rather than squinted at.
- **The procedure's caching convention is applied identically to both states.** Which
  convention is chosen matters less than that it is stated and held constant.
- **No change to the procedure's SQL.** This feature measures the report as feature 003 wrote
  it. Rewriting the query to improve the numbers would make the index measurement meaningless
  and belongs to its own spec if it is ever wanted.

## Out of Scope

Named so the boundary reads as chosen rather than missed.

| Not in this feature | Why |
| --- | --- |
| Any rewrite of the rollup's SQL | The measurement is of the index. A query change in the same feature makes it impossible to say which caused what |
| Additional indexes, statistics tuning, or query hints | `docs/prd.md` §3 commits to one index and the story it tells. A second would need its own before and after |
| A load or throughput harness | `docs/prd.md` §2.2 — the before/after numbers are the performance story, and a harness is a different claim |
| CRUD endpoints and C# enforcement of the domain rules | A later feature. Unrelated to this one |
| Any second report | `docs/prd.md` §2.2 — the pattern is proven once |
| Publishing a target or a service-level figure | This feature reports what is, not what should be. A target nobody measured against is the kind of claim P8 exists to prevent |

## Dependencies

- **Blocked by**: feature 003 for the report to measure, and feature 002 for the deterministic
  seed and its committed reference date — without which the two halves of the comparison are
  not comparable and the whole exercise is decorative.
- **Blocks**: the README's performance section, which currently carries placeholders that
  constitution P8 forbids filling with anything but captured figures.
- **External**: none beyond what the quickstart already requires.
