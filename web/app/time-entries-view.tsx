import { type FormEvent, useEffect, useMemo, useState } from "react";

import {
  type ClientDto,
  getMatter,
  listClients,
  listMattersForClient,
  listTimekeepers,
  type MatterDto,
  partyLabel,
  type TimekeeperDto,
} from "./party-lookups";
import { formatCurrency } from "./reporting";
import {
  deleteTimeEntry,
  formatDurationHours,
  listTimeEntries,
  recordTimeEntry,
  reviseTimeEntry,
  TimeEntryRequestError,
  type DomainRuleViolation,
  type TimeEntryDto,
  type TimeEntryPageSize,
} from "./time-entries-api";
import {
  stateFromPage,
  type TimeEntriesState,
  validateListingRange,
} from "./time-entries-state";
import { TimeEntriesTable } from "./time-entries-table";
import {
  TimeEntryForm,
  type RecordFormValues,
  type ReviseFormValues,
  type TimeEntryFormMode,
} from "./time-entry-form";

const initialFrom = "2026-08-10";
const initialTo = "2026-08-13";

interface TimeEntriesViewProps {
  readonly onUnauthorized: (message: string) => void;
  readonly token: string;
}

export function TimeEntriesView({
  onUnauthorized,
  token,
}: TimeEntriesViewProps): React.JSX.Element {
  const [from, setFrom] = useState(initialFrom);
  const [to, setTo] = useState(initialTo);
  const [userId, setUserId] = useState("");
  const [filterClientId, setFilterClientId] = useState("");
  const [matterId, setMatterId] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<TimeEntryPageSize>(20);
  const [state, setState] = useState<TimeEntriesState>({ kind: "idle" });
  const [validationMessage, setValidationMessage] = useState<string | null>(
    null,
  );
  const [timekeepers, setTimekeepers] = useState<readonly TimekeeperDto[]>([]);
  const [clients, setClients] = useState<readonly ClientDto[]>([]);
  const [filterMatters, setFilterMatters] = useState<readonly MatterDto[]>([]);
  const [formMatters, setFormMatters] = useState<readonly MatterDto[]>([]);
  const [formClientId, setFormClientId] = useState("");
  const [matterNames, setMatterNames] = useState<ReadonlyMap<number, string>>(
    new Map(),
  );
  const [selected, setSelected] = useState<TimeEntryDto | null>(null);
  const [formMode, setFormMode] = useState<TimeEntryFormMode | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [violations, setViolations] = useState<readonly DomainRuleViolation[]>(
    [],
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [capturedRate, setCapturedRate] = useState<number | null>(null);

  const timekeeperNames = useMemo(() => {
    const names = new Map<number, string>();
    for (const timekeeper of timekeepers) {
      names.set(timekeeper.userId, timekeeper.fullName);
    }
    return names;
  }, [timekeepers]);

  const listingPage =
    state.kind === "ready" || state.kind === "empty" ? state.page : null;
  const rows = listingPage?.items ?? [];
  const total = listingPage?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  useEffect(() => {
    void loadDirectoriesAndListing(
      token,
      initialFrom,
      initialTo,
      "",
      "",
      1,
      20,
    );
  }, [token]);

  async function handlePartyError(error: unknown): Promise<boolean> {
    if (error instanceof TimeEntryRequestError && error.kind === "unauthorized") {
      onUnauthorized(error.message);
      setState({ kind: "unauthenticated", message: error.message });
      return true;
    }

    return false;
  }

  async function loadDirectoriesAndListing(
    activeToken: string,
    requestedFrom: string,
    requestedTo: string,
    requestedUserId: string,
    requestedMatterId: string,
    requestedPage: number,
    requestedPageSize: TimeEntryPageSize,
  ): Promise<void> {
    try {
      const [nextTimekeepers, nextClients] = await Promise.all([
        listTimekeepers(activeToken),
        listClients(activeToken),
      ]);
      setTimekeepers(nextTimekeepers);
      setClients(nextClients);
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setState({
        kind: "unavailable",
        message:
          "Time entries could not be loaded. Check that the API is running, then retry.",
      });
      return;
    }

    await loadListing(
      activeToken,
      requestedFrom,
      requestedTo,
      requestedUserId,
      requestedMatterId,
      requestedPage,
      requestedPageSize,
    );
  }

  async function loadListing(
    activeToken: string,
    requestedFrom: string,
    requestedTo: string,
    requestedUserId: string,
    requestedMatterId: string,
    requestedPage: number,
    requestedPageSize: TimeEntryPageSize,
  ): Promise<void> {
    const rangeMessage = validateListingRange(requestedFrom, requestedTo);
    if (rangeMessage !== null) {
      setValidationMessage(rangeMessage);
      setState({ kind: "blocked-range", message: rangeMessage });
      return;
    }

    setValidationMessage(null);
    setState({ kind: "loading" });

    try {
      const pageResult = await listTimeEntries(
        {
          from: requestedFrom,
          matterId:
            requestedMatterId.length === 0
              ? undefined
              : Number(requestedMatterId),
          skip: (requestedPage - 1) * requestedPageSize,
          take: requestedPageSize,
          to: requestedTo,
          userId:
            requestedUserId.length === 0 ? undefined : Number(requestedUserId),
        },
        activeToken,
      );
      const resolved = new Map<number, string>();
      await Promise.all(
        [...new Set(pageResult.items.map((item) => item.matterId))].map(
          async (id) => {
            const matter = await getMatter(id, activeToken);
            if (matter !== null) {
              resolved.set(
                id,
                partyLabel(matter.name, matter.isActive, matter.matterNumber),
              );
            }
          },
        ),
      );
      setMatterNames((current) => new Map([...current, ...resolved]));
      setState(stateFromPage(pageResult));
      if (
        selected !== null &&
        pageResult.items.every(
          (item) => item.timeEntryId !== selected.timeEntryId,
        )
      ) {
        setSelected(null);
      }
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setState({
        kind: "unavailable",
        message:
          "Time entries could not be loaded. Check that the API is running, then retry.",
      });
    }
  }

  async function changeFilterClient(clientId: string): Promise<void> {
    setFilterClientId(clientId);
    setMatterId("");
    setPage(1);
    if (clientId.length === 0) {
      setFilterMatters([]);
      return;
    }

    try {
      setFilterMatters(await listMattersForClient(Number(clientId), token));
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setState({
        kind: "unavailable",
        message: "The matter list could not be loaded.",
      });
    }
  }

  async function changeFormClient(clientId: string): Promise<void> {
    setFormClientId(clientId);
    if (clientId.length === 0) {
      setFormMatters([]);
      return;
    }

    try {
      setFormMatters(await listMattersForClient(Number(clientId), token));
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setFormError("The matter list could not be loaded.");
    }
  }

  function applyFilters(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setPage(1);
    void loadListing(token, from, to, userId, matterId, 1, pageSize);
  }

  function changePageSize(nextPageSize: TimeEntryPageSize): void {
    setPageSize(nextPageSize);
    setPage(1);
    void loadListing(token, from, to, userId, matterId, 1, nextPageSize);
  }

  function openRecord(): void {
    setFormMode("record");
    setFormError(null);
    setViolations([]);
    setCapturedRate(null);
    setFormClientId(filterClientId);
    setFormMatters(filterMatters);
  }

  function openRevise(): void {
    if (selected === null) {
      return;
    }

    setFormMode("revise");
    setFormError(null);
    setViolations([]);
    setCapturedRate(selected.hourlyRateSnapshot);
    void prepareFormMatters(selected.matterId);
  }

  function openDelete(): void {
    if (selected === null) {
      return;
    }

    setFormMode("delete");
    setFormError(null);
    setViolations([]);
  }

  async function prepareFormMatters(selectedMatterId: number): Promise<void> {
    const known = await getMatter(selectedMatterId, token);
    if (known === null) {
      setFormClientId("");
      setFormMatters([]);
      return;
    }

    setFormClientId(String(known.clientId));
    try {
      setFormMatters(await listMattersForClient(known.clientId, token));
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setFormMatters([known]);
    }
  }

  async function submitRecord(values: RecordFormValues): Promise<void> {
    setIsSubmitting(true);
    setFormError(null);
    setViolations([]);

    try {
      const recorded = await recordTimeEntry(values, token);
      const nextFrom = recorded.workDate < from ? recorded.workDate : from;
      const nextTo = recorded.workDate > to ? recorded.workDate : to;
      setFrom(nextFrom);
      setTo(nextTo);
      setCapturedRate(recorded.hourlyRateSnapshot);
      setSelected(recorded);
      setFormMode(null);
      setPage(1);
      await loadListing(token, nextFrom, nextTo, userId, matterId, 1, pageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitRevise(values: ReviseFormValues): Promise<void> {
    if (selected === null) {
      return;
    }

    setIsSubmitting(true);
    setFormError(null);
    setViolations([]);

    try {
      const revised = await reviseTimeEntry(selected.timeEntryId, values, token);
      setCapturedRate(revised.hourlyRateSnapshot);
      setSelected(revised);
      setFormMode(null);
      await loadListing(token, from, to, userId, matterId, page, pageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function confirmDelete(): Promise<void> {
    if (selected === null) {
      return;
    }

    setIsSubmitting(true);
    setFormError(null);

    try {
      await deleteTimeEntry(selected.timeEntryId, token);
      setSelected(null);
      setFormMode(null);
      await loadListing(token, from, to, userId, matterId, page, pageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  function applyWriteError(error: unknown): void {
    if (!(error instanceof TimeEntryRequestError)) {
      setFormError("The service could not complete that request.");
      return;
    }

    switch (error.kind) {
      case "unauthorized":
        onUnauthorized(error.message);
        return;
      case "refused":
        setViolations(error.violations);
        setFormError(null);
        return;
      case "missing":
        setFormMode(null);
        setSelected(null);
        setState({ kind: "missing", message: error.message });
        return;
      case "unavailable":
        setFormError(error.message);
        return;
      default: {
        const unhandled: never = error.kind;
        return unhandled;
      }
    }
  }

  const periodPrefix =
    listingPage === null
      ? state.kind === "loading"
        ? "Loading range"
        : "Selected range (not current)"
      : "Current range";

  return (
    <>
      <header className="report-header">
        <div>
          <p className="eyebrow">Time entries</p>
          <h1 className="report-title">Recorded time</h1>
          <p className="period-label">
            {periodPrefix}: {from || "start required"} — {to || "end required"}
          </p>
        </div>
        <button className="primary-button" onClick={openRecord} type="button">
          Record time
        </button>
      </header>

      <form className="report-controls entries-controls" onSubmit={applyFilters}>
        <div className="field">
          <label htmlFor="entries-from">From</label>
          <input
            id="entries-from"
            onChange={(event) => setFrom(event.target.value)}
            type="date"
            value={from}
          />
        </div>
        <div className="field">
          <label htmlFor="entries-to">To</label>
          <input
            id="entries-to"
            onChange={(event) => setTo(event.target.value)}
            type="date"
            value={to}
          />
        </div>
        <div className="field">
          <label htmlFor="entries-timekeeper">Timekeeper</label>
          <select
            id="entries-timekeeper"
            onChange={(event) => {
              setUserId(event.target.value);
              setPage(1);
            }}
            value={userId}
          >
            <option value="">All timekeepers</option>
            {timekeepers.map((timekeeper) => (
              <option key={timekeeper.userId} value={timekeeper.userId}>
                {partyLabel(timekeeper.fullName, timekeeper.isActive)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="entries-client">Client (for matter list)</label>
          <select
            id="entries-client"
            onChange={(event) => void changeFilterClient(event.target.value)}
            value={filterClientId}
          >
            <option value="">All clients</option>
            {clients.map((client) => (
              <option key={client.clientId} value={client.clientId}>
                {partyLabel(client.name, client.isActive, client.clientCode)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="entries-matter">Matter</label>
          <select
            id="entries-matter"
            onChange={(event) => {
              setMatterId(event.target.value);
              setPage(1);
            }}
            value={matterId}
          >
            <option value="">All matters</option>
            {filterMatters.map((matter) => (
              <option key={matter.matterId} value={matter.matterId}>
                {partyLabel(matter.name, matter.isActive, matter.matterNumber)}
              </option>
            ))}
          </select>
        </div>
        <button className="primary-button" type="submit">
          Apply
        </button>
        {validationMessage !== null && (
          <p className="field-error" role="alert">
            {validationMessage}
          </p>
        )}
      </form>

      <TimeEntriesStatus
        onRetry={() =>
          void loadListing(token, from, to, userId, matterId, page, pageSize)
        }
        state={state}
      />

      <div className="entries-workspace">
        {state.kind === "ready" && (
          <TimeEntriesTable
            matterNames={matterNames}
            onNextPage={() => {
              const nextPage = Math.min(page + 1, pageCount);
              setPage(nextPage);
              void loadListing(
                token,
                from,
                to,
                userId,
                matterId,
                nextPage,
                pageSize,
              );
            }}
            onPageSizeChange={changePageSize}
            onPreviousPage={() => {
              const nextPage = Math.max(page - 1, 1);
              setPage(nextPage);
              void loadListing(
                token,
                from,
                to,
                userId,
                matterId,
                nextPage,
                pageSize,
              );
            }}
            onSelect={setSelected}
            page={page}
            pageCount={pageCount}
            pageSize={pageSize}
            rows={rows}
            selectedId={selected?.timeEntryId ?? null}
            timekeeperNames={timekeeperNames}
            total={total}
          />
        )}

        {formMode !== null ? (
          <TimeEntryForm
            capturedRate={formMode === "record" ? capturedRate : selected?.hourlyRateSnapshot ?? null}
            clients={clients}
            entry={selected}
            fieldError={formError}
            isSubmitting={isSubmitting}
            key={`${formMode}-${selected?.timeEntryId ?? "new"}`}
            matters={formMatters}
            mode={formMode}
            onCancel={() => {
              setFormMode(null);
              setViolations([]);
              setFormError(null);
            }}
            onConfirmDelete={() => void confirmDelete()}
            onPickerClientChange={(clientId) => void changeFormClient(clientId)}
            onSubmitRecord={(values) => void submitRecord(values)}
            onSubmitRevise={(values) => void submitRevise(values)}
            pickerClientId={formClientId}
            timekeeperName={
              selected === null
                ? ""
                : (timekeeperNames.get(selected.userId) ?? `#${selected.userId}`)
            }
            timekeepers={timekeepers}
            violations={violations}
          />
        ) : (
          selected !== null && (
            <TimeEntryDetail
              entry={selected}
              matterLabel={
                matterNames.get(selected.matterId) ?? `#${selected.matterId}`
              }
              onClose={() => setSelected(null)}
              onDelete={openDelete}
              onEdit={openRevise}
              timekeeperLabel={
                timekeeperNames.get(selected.userId) ?? `#${selected.userId}`
              }
            />
          )
        )}
      </div>
    </>
  );
}

interface TimeEntriesStatusProps {
  readonly onRetry: () => void;
  readonly state: TimeEntriesState;
}

function TimeEntriesStatus({
  onRetry,
  state,
}: TimeEntriesStatusProps): React.JSX.Element | null {
  switch (state.kind) {
    case "idle":
    case "ready":
    case "blocked-range":
      return null;
    case "loading":
      return (
        <div
          aria-label="Loading time entries"
          aria-valuetext="Loading"
          className="loading-bar"
          role="progressbar"
        />
      );
    case "empty":
      return (
        <section className="status-panel status-empty" role="status">
          <div>
            <h2>No matching time entries</h2>
            <p>
              The listing completed successfully, but nothing matches these
              filters. Try a broader range.
            </p>
          </div>
        </section>
      );
    case "unauthenticated":
      return (
        <section className="status-panel status-error" role="alert">
          <div>
            <h2>Development session expired</h2>
            <p>{state.message}</p>
          </div>
        </section>
      );
    case "unavailable":
      return (
        <section className="status-panel status-error" role="alert">
          <div>
            <h2>Time entries unavailable</h2>
            <p>{state.message}</p>
          </div>
          <div className="status-actions">
            <button className="secondary-button" onClick={onRetry} type="button">
              Try again
            </button>
          </div>
        </section>
      );
    case "missing":
      return (
        <section className="status-panel status-info" role="status">
          <div>
            <h2>Time entry not found</h2>
            <p>{state.message}</p>
          </div>
        </section>
      );
    default: {
      const unhandledState: never = state;
      return unhandledState;
    }
  }
}

interface TimeEntryDetailProps {
  readonly entry: TimeEntryDto;
  readonly matterLabel: string;
  readonly onClose: () => void;
  readonly onDelete: () => void;
  readonly onEdit: () => void;
  readonly timekeeperLabel: string;
}

function TimeEntryDetail({
  entry,
  matterLabel,
  onClose,
  onDelete,
  onEdit,
  timekeeperLabel,
}: TimeEntryDetailProps): React.JSX.Element {
  return (
    <section className="detail-pane" aria-labelledby="entry-detail-title">
      <div className="detail-header">
        <h2 id="entry-detail-title">Time entry details</h2>
        <button
          aria-label="Close details"
          className="secondary-button"
          onClick={onClose}
          type="button"
        >
          Close
        </button>
      </div>
      <dl className="detail-list">
        <div>
          <dt>Work date</dt>
          <dd>{entry.workDate}</dd>
        </div>
        <div>
          <dt>Narrative</dt>
          <dd>{entry.narrative}</dd>
        </div>
        <div>
          <dt>Timekeeper</dt>
          <dd>{timekeeperLabel}</dd>
        </div>
        <div>
          <dt>Matter</dt>
          <dd>{matterLabel}</dd>
        </div>
        <div>
          <dt>Duration</dt>
          <dd>
            {formatDurationHours(entry.durationMinutes)} h (
            {entry.durationMinutes} min)
          </dd>
        </div>
        <div>
          <dt>Billable</dt>
          <dd>{entry.isBillable ? "Billable" : "Not billable"}</dd>
        </div>
        <div>
          <dt>Captured rate</dt>
          <dd>{formatCurrency(entry.hourlyRateSnapshot)}</dd>
        </div>
        <div>
          <dt>Recorded</dt>
          <dd>{entry.createdAtUtc}</dd>
        </div>
        <div>
          <dt>Last revised</dt>
          <dd>{entry.updatedAtUtc ?? "Never"}</dd>
        </div>
      </dl>
      <div className="form-actions">
        <button className="secondary-button" onClick={onDelete} type="button">
          Delete
        </button>
        <button className="primary-button" onClick={onEdit} type="button">
          Edit entry
        </button>
      </div>
    </section>
  );
}
