import { Plus } from "lucide-react";
import { type FormEvent, useEffect, useMemo, useState } from "react";

import {
  AlertBanner,
  btn,
  field,
  LoadingBar,
  PageHeader,
} from "@/components/kit";
import { cn } from "@/lib/utils";

import { DetailPanel } from "./detail-form";
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

  return (
    <>
      <PageHeader
        subtitle={`${from || "start required"} – ${to || "end required"}`}
        title="Recorded time"
      >
        <button className={btn.primary} onClick={openRecord} type="button">
          <Plus className="size-3.5" /> Record time
        </button>
      </PageHeader>

      <form className="flex flex-wrap items-end gap-2" onSubmit={applyFilters}>
        <label className="block">
          <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">From</span>
          <input className={cn(field, "w-auto")} id="entries-from" onChange={(event) => setFrom(event.target.value)} type="date" value={from} />
        </label>
        <label className="block">
          <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">To</span>
          <input className={cn(field, "w-auto")} id="entries-to" onChange={(event) => setTo(event.target.value)} type="date" value={to} />
        </label>
        <label className="block">
          <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">Timekeeper</span>
          <select className={cn(field, "w-auto")} id="entries-timekeeper" onChange={(event) => { setUserId(event.target.value); setPage(1); }} value={userId}>
            <option value="">All timekeepers</option>
            {timekeepers.map((timekeeper) => (
              <option key={timekeeper.userId} value={timekeeper.userId}>
                {partyLabel(timekeeper.fullName, timekeeper.isActive)}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">Client</span>
          <select className={cn(field, "w-auto")} id="entries-client" onChange={(event) => void changeFilterClient(event.target.value)} value={filterClientId}>
            <option value="">All clients</option>
            {clients.map((client) => (
              <option key={client.clientId} value={client.clientId}>
                {partyLabel(client.name, client.isActive, client.clientCode)}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">Matter</span>
          <select className={cn(field, "w-auto")} id="entries-matter" onChange={(event) => { setMatterId(event.target.value); setPage(1); }} value={matterId}>
            <option value="">All matters</option>
            {filterMatters.map((matter) => (
              <option key={matter.matterId} value={matter.matterId}>
                {partyLabel(matter.name, matter.isActive, matter.matterNumber)}
              </option>
            ))}
          </select>
        </label>
        <button className={btn.primary} type="submit">
          Apply
        </button>
        {validationMessage !== null && (
          <p className="w-full text-[12px] font-medium text-red-600" role="alert">
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

      <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
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
      return null;
    case "loading":
      return <LoadingBar />;
    case "blocked-range":
      return (
        <AlertBanner title="No time entries shown" variant="error">
          Fix the date range above — its start can&rsquo;t be after its end — to
          see the listing again.
        </AlertBanner>
      );
    case "empty":
      return (
        <AlertBanner title="No matching time entries" variant="empty">
          The listing completed successfully, but nothing matches these filters.
          Try a broader range.
        </AlertBanner>
      );
    case "unauthenticated":
      return (
        <AlertBanner title="Development session expired" variant="error">
          {state.message}
        </AlertBanner>
      );
    case "unavailable":
      return (
        <div className="space-y-3">
          <AlertBanner title="Time entries unavailable" variant="error">
            {state.message}
          </AlertBanner>
          <button className={btn.ghost} onClick={onRetry} type="button">
            Try again
          </button>
        </div>
      );
    case "missing":
      return (
        <AlertBanner title="Time entry not found" variant="info">
          {state.message}
        </AlertBanner>
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
    <DetailPanel
      actions={
        <>
          <button className={btn.danger} onClick={onDelete} type="button">
            Delete
          </button>
          <button className={btn.primary} onClick={onEdit} type="button">
            Edit entry
          </button>
        </>
      }
      headerAction={
        <button
          aria-label="Close details"
          className="rounded border border-white/25 px-2 py-1 text-[11px] font-medium text-white/80 transition-colors hover:bg-white/10"
          onClick={onClose}
          type="button"
        >
          Close
        </button>
      }
      title="Time entry details"
      titleId="entry-detail-title"
    >
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
    </DetailPanel>
  );
}
