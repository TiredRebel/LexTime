import { Plus } from "lucide-react";
import { type FormEvent, useEffect, useState } from "react";

import {
  AlertBanner,
  btn,
  Card,
  field,
  LoadingBar,
  PageHeader,
  StatStrip,
  td,
  th,
} from "@/components/kit";
import { cn } from "@/lib/utils";

import {
  ClientForm,
  type ClientFormMode,
  type CorrectClientValues,
  type RegisterClientValues,
} from "./client-form";
import { ClientsTable } from "./clients-table";
import { DetailPanel } from "./detail-form";
import {
  MatterForm,
  type CorrectMatterValues,
  type MatterFormMode,
  type OpenMatterValues,
} from "./matter-form";
import {
  correctClient,
  correctMatter,
  formatActiveFlag,
  formatUtcDate,
  listClients,
  listMattersForClient,
  openMatter,
  partyPageSizes,
  PartyRequestError,
  registerClient,
  type ClientDto,
  type ClientStatusFilter,
  type MatterDto,
  type PartyPageSize,
} from "./parties-api";
import {
  stateFromClientPage,
  stateFromMatterPage,
  type ClientsState,
  type MattersState,
} from "./parties-state";

interface ClientsViewProps {
  readonly onUnauthorized: (message: string) => void;
  readonly token: string;
}

export function ClientsView({
  onUnauthorized,
  token,
}: ClientsViewProps): React.JSX.Element {
  const [status, setStatus] = useState<ClientStatusFilter>("all");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PartyPageSize>(20);
  const [state, setState] = useState<ClientsState>({ kind: "idle" });
  const [selected, setSelected] = useState<ClientDto | null>(null);
  const [mattersState, setMattersState] = useState<MattersState>({
    kind: "idle",
  });
  const [matterPage, setMatterPage] = useState(1);
  const [matterPageSize, setMatterPageSize] = useState<PartyPageSize>(20);
  const [selectedMatter, setSelectedMatter] = useState<MatterDto | null>(null);
  const [clientFormMode, setClientFormMode] = useState<ClientFormMode | null>(
    null,
  );
  const [matterFormMode, setMatterFormMode] = useState<MatterFormMode | null>(
    null,
  );
  const [formError, setFormError] = useState<string | null>(null);
  const [conflictField, setConflictField] = useState<string | null>(null);
  const [conflictValue, setConflictValue] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const listingPage =
    state.kind === "ready" || state.kind === "empty" ? state.page : null;
  const rows = listingPage?.items ?? [];
  const total = listingPage?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const matterListing =
    mattersState.kind === "ready" || mattersState.kind === "empty"
      ? mattersState.page
      : null;
  const matterRows = matterListing?.items ?? [];
  const matterTotal = matterListing?.total ?? 0;
  const matterPageCount = Math.max(1, Math.ceil(matterTotal / matterPageSize));

  useEffect(() => {
    void loadClients(token, "all", 1, 20);
  }, [token]);

  async function handlePartyError(error: unknown): Promise<boolean> {
    if (error instanceof PartyRequestError && error.kind === "unauthorized") {
      onUnauthorized(error.message);
      setState({ kind: "unauthenticated", message: error.message });
      return true;
    }

    return false;
  }

  async function loadClients(
    activeToken: string,
    requestedStatus: ClientStatusFilter,
    requestedPage: number,
    requestedPageSize: PartyPageSize,
  ): Promise<void> {
    setState({ kind: "loading" });

    try {
      const pageResult = await listClients(
        {
          skip: (requestedPage - 1) * requestedPageSize,
          status: requestedStatus,
          take: requestedPageSize,
        },
        activeToken,
      );
      setState(stateFromClientPage(pageResult));
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      setState({
        kind: "unavailable",
        message:
          "Clients could not be loaded. Check that the API is running, then retry.",
      });
    }
  }

  async function loadMatters(
    clientId: number,
    requestedPage: number,
    requestedPageSize: PartyPageSize,
  ): Promise<void> {
    setMattersState({ kind: "loading" });

    try {
      const pageResult = await listMattersForClient(
        {
          clientId,
          skip: (requestedPage - 1) * requestedPageSize,
          take: requestedPageSize,
        },
        token,
      );
      setMattersState(stateFromMatterPage(pageResult));
    } catch (error) {
      if (await handlePartyError(error)) {
        return;
      }

      if (error instanceof PartyRequestError && error.kind === "missing") {
        setSelected(null);
        setSelectedMatter(null);
        setMattersState({ kind: "missing-parent", message: error.message });
        setState({
          kind: "missing",
          message: "That client is no longer there.",
        });
        return;
      }

      setMattersState({
        kind: "unavailable",
        message:
          "Matters could not be loaded. Check that the API is running, then retry.",
      });
    }
  }

  function applyStatus(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setPage(1);
    setSelected(null);
    setSelectedMatter(null);
    setClientFormMode(null);
    setMatterFormMode(null);
    void loadClients(token, status, 1, pageSize);
  }

  function selectClient(client: ClientDto): void {
    setSelected(client);
    setSelectedMatter(null);
    setClientFormMode(null);
    setMatterFormMode(null);
    setFormError(null);
    setConflictField(null);
    setConflictValue(null);
    setMatterPage(1);
    void loadMatters(client.clientId, 1, matterPageSize);
  }

  function changeClientPageSize(nextPageSize: PartyPageSize): void {
    setPageSize(nextPageSize);
    setPage(1);
    void loadClients(token, status, 1, nextPageSize);
  }

  function changeMatterPageSize(nextPageSize: PartyPageSize): void {
    if (selected === null) {
      return;
    }

    setMatterPageSize(nextPageSize);
    setMatterPage(1);
    void loadMatters(selected.clientId, 1, nextPageSize);
  }

  function clearFormMessages(): void {
    setFormError(null);
    setConflictField(null);
    setConflictValue(null);
  }

  function applyWriteError(error: unknown): void {
    if (!(error instanceof PartyRequestError)) {
      setFormError("The directory is temporarily unavailable.");
      return;
    }

    switch (error.kind) {
      case "unauthorized":
        onUnauthorized(error.message);
        setState({ kind: "unauthenticated", message: error.message });
        return;
      case "conflict":
        setFormError(error.message);
        setConflictField(error.conflictingField);
        setConflictValue(error.conflictingValue);
        return;
      case "malformed":
        setFormError(error.message);
        setConflictField(null);
        setConflictValue(null);
        return;
      case "missing-parent":
        setClientFormMode(null);
        setMatterFormMode(null);
        setSelected(null);
        setSelectedMatter(null);
        setState({ kind: "missing", message: error.message });
        setMattersState({ kind: "missing-parent", message: error.message });
        return;
      case "missing":
        setClientFormMode(null);
        setMatterFormMode(null);
        setSelected(null);
        setSelectedMatter(null);
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

  async function submitRegister(values: RegisterClientValues): Promise<void> {
    setIsSubmitting(true);
    clearFormMessages();

    try {
      const created = await registerClient(values, token);
      setClientFormMode(null);
      setSelected(created);
      setSelectedMatter(null);
      setStatus("all");
      setPage(1);
      await loadClients(token, "all", 1, pageSize);
      await loadMatters(created.clientId, 1, matterPageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitCorrectClient(
    values: CorrectClientValues,
  ): Promise<void> {
    if (selected === null) {
      return;
    }

    setIsSubmitting(true);
    clearFormMessages();

    try {
      const revised = await correctClient(selected.clientId, values, token);
      setSelected(revised);
      setClientFormMode(null);
      await loadClients(token, status, page, pageSize);
      await loadMatters(revised.clientId, matterPage, matterPageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitOpenMatter(values: OpenMatterValues): Promise<void> {
    if (selected === null) {
      return;
    }

    setIsSubmitting(true);
    clearFormMessages();

    try {
      const created = await openMatter(selected.clientId, values, token);
      setMatterFormMode(null);
      setSelectedMatter(created);
      setMatterPage(1);
      await loadMatters(selected.clientId, 1, matterPageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  async function submitCorrectMatter(
    values: CorrectMatterValues,
  ): Promise<void> {
    if (selected === null || selectedMatter === null) {
      return;
    }

    setIsSubmitting(true);
    clearFormMessages();

    try {
      const revised = await correctMatter(selectedMatter.matterId, values, token);
      setSelectedMatter(revised);
      setMatterFormMode(null);
      await loadMatters(selected.clientId, matterPage, matterPageSize);
    } catch (error) {
      applyWriteError(error);
    } finally {
      setIsSubmitting(false);
    }
  }

  const statusLabel =
    status === "all" ? "All" : status === "active" ? "Active" : "Inactive";

  return (
    <>
      <PageHeader title="Clients">
        <select
          className={cn(field, "w-auto")}
          id="client-status"
          onChange={(event) => {
            const nextStatus = event.target.value as ClientStatusFilter;
            setStatus(nextStatus);
            setPage(1);
            setSelected(null);
            setSelectedMatter(null);
            void loadClients(token, nextStatus, 1, pageSize);
          }}
          value={status}
        >
          <option value="all">All statuses</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </select>
        <button
          className={btn.primary}
          onClick={() => {
            setClientFormMode("register");
            setMatterFormMode(null);
            clearFormMessages();
          }}
          type="button"
        >
          <Plus className="size-3.5" /> Add client
        </button>
      </PageHeader>

      <StatStrip
        cols={3}
        items={[
          {
            label: "Filter",
            value: statusLabel,
          },
          {
            label: "Shown",
            value: String(total),
          },
          {
            label: "Page",
            value: `${page} / ${pageCount}`,
          },
        ]}
      />

      <DirectoryStatus
        emptyTitle="No matching clients"
        emptyBody="The listing completed successfully, but nothing matches this status filter."
        missingTitle="Client not found"
        onRetry={() => void loadClients(token, status, page, pageSize)}
        state={state}
        unavailableTitle="Clients unavailable"
      />

      <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
        {state.kind === "ready" && (
          <ClientsTable
            onNextPage={() => {
              const nextPage = Math.min(page + 1, pageCount);
              setPage(nextPage);
              void loadClients(token, status, nextPage, pageSize);
            }}
            onPageSizeChange={changeClientPageSize}
            onPreviousPage={() => {
              const nextPage = Math.max(page - 1, 1);
              setPage(nextPage);
              void loadClients(token, status, nextPage, pageSize);
            }}
            onSelect={selectClient}
            page={page}
            pageCount={pageCount}
            pageSize={pageSize}
            rows={rows}
            selectedId={selected?.clientId ?? null}
            total={total}
          />
        )}

        <div className="space-y-5">
          {clientFormMode !== null ? (
            <ClientForm
              client={selected}
              conflictField={conflictField}
              conflictValue={conflictValue}
              fieldError={formError}
              isSubmitting={isSubmitting}
              key={`${clientFormMode}-${selected?.clientId ?? "new"}`}
              mode={clientFormMode}
              onCancel={() => {
                setClientFormMode(null);
                clearFormMessages();
              }}
              onSubmitCorrect={(values) => void submitCorrectClient(values)}
              onSubmitRegister={(values) => void submitRegister(values)}
            />
          ) : (
            selected !== null && (
              <ClientDetail
                client={selected}
                onClose={() => {
                  setSelected(null);
                  setSelectedMatter(null);
                  setMatterFormMode(null);
                }}
                onCorrect={() => {
                  setClientFormMode("correct");
                  setMatterFormMode(null);
                  clearFormMessages();
                }}
                onOpenMatter={() => {
                  setMatterFormMode("open");
                  setClientFormMode(null);
                  clearFormMessages();
                }}
              />
            )
          )}

          {selected !== null && clientFormMode === null && (
            <MattersPanel
              client={selected}
              matterPage={matterPage}
              matterPageCount={matterPageCount}
              matterPageSize={matterPageSize}
              matterRows={matterRows}
              matterTotal={matterTotal}
              mattersState={mattersState}
              onCorrectMatter={() => {
                setMatterFormMode("correct");
                clearFormMessages();
              }}
              onMatterPageSizeChange={changeMatterPageSize}
              onNextMatterPage={() => {
                const nextPage = Math.min(matterPage + 1, matterPageCount);
                setMatterPage(nextPage);
                void loadMatters(selected.clientId, nextPage, matterPageSize);
              }}
              onPreviousMatterPage={() => {
                const nextPage = Math.max(matterPage - 1, 1);
                setMatterPage(nextPage);
                void loadMatters(selected.clientId, nextPage, matterPageSize);
              }}
              onRetryMatters={() =>
                void loadMatters(selected.clientId, matterPage, matterPageSize)
              }
              onSelectMatter={setSelectedMatter}
              selectedMatter={selectedMatter}
            />
          )}

          {matterFormMode !== null && selected !== null && (
            <MatterForm
              clientLabel={`${selected.clientCode} · ${selected.name}`}
              conflictField={conflictField}
              conflictValue={conflictValue}
              fieldError={formError}
              isSubmitting={isSubmitting}
              key={`${matterFormMode}-${selectedMatter?.matterId ?? "new"}`}
              matter={selectedMatter}
              mode={matterFormMode}
              onCancel={() => {
                setMatterFormMode(null);
                clearFormMessages();
              }}
              onSubmitCorrect={(values) => void submitCorrectMatter(values)}
              onSubmitOpen={(values) => void submitOpenMatter(values)}
            />
          )}
        </div>
      </div>
    </>
  );
}

interface ClientDetailProps {
  readonly client: ClientDto;
  readonly onClose: () => void;
  readonly onCorrect: () => void;
  readonly onOpenMatter: () => void;
}

function ClientDetail({
  client,
  onClose,
  onCorrect,
  onOpenMatter,
}: ClientDetailProps): React.JSX.Element {
  return (
    <DetailPanel
      actions={
        <>
          <button className={btn.ghost} onClick={onCorrect} type="button">
            Edit client
          </button>
          <button className={btn.primary} onClick={onOpenMatter} type="button">
            Open matter
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
      title={client.name}
      titleId="client-detail-title"
    >
      <dl className="detail-list">
        <div>
          <dt>Client code</dt>
          <dd>{client.clientCode}</dd>
        </div>
        <div>
          <dt>Active</dt>
          <dd>{formatActiveFlag(client.isActive)}</dd>
        </div>
        <div>
          <dt>Registered</dt>
          <dd>{formatUtcDate(client.createdAtUtc)}</dd>
        </div>
      </dl>
    </DetailPanel>
  );
}

interface MattersPanelProps {
  readonly client: ClientDto;
  readonly matterPage: number;
  readonly matterPageCount: number;
  readonly matterPageSize: PartyPageSize;
  readonly matterRows: readonly MatterDto[];
  readonly matterTotal: number;
  readonly mattersState: MattersState;
  readonly onCorrectMatter: () => void;
  readonly onMatterPageSizeChange: (pageSize: PartyPageSize) => void;
  readonly onNextMatterPage: () => void;
  readonly onPreviousMatterPage: () => void;
  readonly onRetryMatters: () => void;
  readonly onSelectMatter: (matter: MatterDto) => void;
  readonly selectedMatter: MatterDto | null;
}

function MattersPanel({
  client,
  matterPage,
  matterPageCount,
  matterPageSize,
  matterRows,
  matterTotal,
  mattersState,
  onCorrectMatter,
  onMatterPageSizeChange,
  onNextMatterPage,
  onPreviousMatterPage,
  onRetryMatters,
  onSelectMatter,
  selectedMatter,
}: MattersPanelProps): React.JSX.Element {
  const firstRow =
    matterTotal === 0 ? 0 : (matterPage - 1) * matterPageSize + 1;
  const lastRow = Math.min(matterPage * matterPageSize, matterTotal);

  return (
    <div className="nested-matters">
      <DirectoryStatus
        emptyTitle="No matters for this client"
        emptyBody="This client has no matters yet. That is an empty success, not a missing client."
        missingTitle="Client not found"
        onRetry={onRetryMatters}
        state={mattersState}
        unavailableTitle="Matters unavailable"
      />
      {mattersState.kind === "ready" && (
        <Card
          meta={`${firstRow}–${lastRow} of ${matterTotal} matching`}
          title={`Matters for ${client.clientCode}`}
        >
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-slate-200">
                <th className={th}>Number</th>
                <th className={th}>Matter name</th>
                <th className={cn(th, "text-center")}>Default billable</th>
                <th className={cn(th, "text-center")}>Active</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {matterRows.map((row) => {
                const isSelected = row.matterId === selectedMatter?.matterId;
                return (
                  <tr
                    key={row.matterId}
                    className={cn(
                      "cursor-pointer transition-colors duration-150 hover:bg-slate-50",
                      isSelected && "bg-brand/[0.07]",
                    )}
                    onClick={() => onSelectMatter(row)}
                  >
                    <td className={cn(td, "font-semibold", isSelected ? "text-brand" : "")}>
                      {row.matterNumber}
                    </td>
                    <td className={td}>{row.name}</td>
                    <td className={cn(td, "text-center text-slate-600")}>
                      {row.isBillableByDefault ? "Yes" : "No"}
                    </td>
                    <td className={cn(td, "text-center text-[12px] font-semibold", row.isActive ? "text-brand" : "text-slate-400")}>
                      {formatActiveFlag(row.isActive)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-2.5">
            <label className="flex items-center gap-2 text-[11px] text-slate-500">
              Rows per page
              <select
                className="rounded border border-slate-300 bg-white px-2 py-1 text-[13px]"
                id="matter-page-size"
                onChange={(event) => {
                  const parsedValue = Number(event.target.value);
                  if (
                    parsedValue === 20 ||
                    parsedValue === 50 ||
                    parsedValue === 100
                  ) {
                    onMatterPageSizeChange(parsedValue);
                  }
                }}
                value={matterPageSize}
              >
                {partyPageSizes.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
            <span aria-live="polite" className="text-[11px] tabular-nums text-slate-500">
              Page {matterPage} of {matterPageCount}
            </span>
            <div className="flex gap-2">
              <button
                className="rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] disabled:opacity-50"
                disabled={matterPage === 1}
                onClick={onPreviousMatterPage}
                type="button"
              >
                Previous
              </button>
              <button
                className="rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] disabled:opacity-50"
                disabled={matterPage === matterPageCount}
                onClick={onNextMatterPage}
                type="button"
              >
                Next
              </button>
            </div>
          </div>
        </Card>
      )}
      {selectedMatter !== null && (
        <DetailPanel
          headerAction={
            <button
              className={cn(btn.ghost, "border-white/25 text-white/80 hover:bg-white/10")}
              onClick={onCorrectMatter}
              type="button"
            >
              Edit matter
            </button>
          }
          title={selectedMatter.name}
          titleId="matter-detail-title"
        >
          <dl className="detail-list">
            <div>
              <dt>Matter number</dt>
              <dd>{selectedMatter.matterNumber}</dd>
            </div>
            <div>
              <dt>Client</dt>
              <dd>
                {client.clientCode} · {client.name}
              </dd>
            </div>
            <div>
              <dt>Default billable</dt>
              <dd>{selectedMatter.isBillableByDefault ? "Yes" : "No"}</dd>
            </div>
            <div>
              <dt>Active</dt>
              <dd>{formatActiveFlag(selectedMatter.isActive)}</dd>
            </div>
          </dl>
        </DetailPanel>
      )}
    </div>
  );
}

interface DirectoryStatusProps {
  readonly emptyBody: string;
  readonly emptyTitle: string;
  readonly missingTitle: string;
  readonly onRetry: () => void;
  readonly state: ClientsState | MattersState;
  readonly unavailableTitle: string;
}

function DirectoryStatus({
  emptyBody,
  emptyTitle,
  missingTitle,
  onRetry,
  state,
  unavailableTitle,
}: DirectoryStatusProps): React.JSX.Element | null {
  switch (state.kind) {
    case "idle":
    case "ready":
      return null;
    case "loading":
      return <LoadingBar />;
    case "empty":
      return (
        <AlertBanner title={emptyTitle} variant="empty">
          {emptyBody}
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
          <AlertBanner title={unavailableTitle} variant="error">
            {state.message}
          </AlertBanner>
          <button className={btn.ghost} onClick={onRetry} type="button">
            Try again
          </button>
        </div>
      );
    case "missing":
    case "missing-parent":
      return (
        <AlertBanner title={missingTitle} variant="info">
          {state.message}
        </AlertBanner>
      );
    default: {
      const unhandledState: never = state;
      return unhandledState;
    }
  }
}
