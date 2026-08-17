import { type FormEvent, useEffect, useState } from "react";

import {
  ClientForm,
  type ClientFormMode,
  type CorrectClientValues,
  type RegisterClientValues,
} from "./client-form";
import { ClientsTable } from "./clients-table";
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
      <header className="report-header">
        <div>
          <p className="eyebrow">Directories</p>
          <h1 className="report-title">Clients</h1>
          <p className="period-label">Status filter: {statusLabel}</p>
        </div>
        <button
          className="primary-button"
          onClick={() => {
            setClientFormMode("register");
            setMatterFormMode(null);
            clearFormMessages();
          }}
          type="button"
        >
          Add client
        </button>
      </header>

      <form className="report-controls directory-controls" onSubmit={applyStatus}>
        <div className="field">
          <label htmlFor="client-status">Status</label>
          <select
            id="client-status"
            onChange={(event) =>
              setStatus(event.target.value as ClientStatusFilter)
            }
            value={status}
          >
            <option value="all">All</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>
        <button className="primary-button" type="submit">
          Apply
        </button>
      </form>

      <DirectoryStatus
        emptyTitle="No matching clients"
        emptyBody="The listing completed successfully, but nothing matches this status filter."
        missingTitle="Client not found"
        onRetry={() => void loadClients(token, status, page, pageSize)}
        state={state}
        unavailableTitle="Clients unavailable"
      />

      <div className="entries-workspace">
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

        <div className="directory-side">
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
    <section className="detail-pane" aria-labelledby="client-detail-title">
      <div className="detail-header">
        <h2 id="client-detail-title">{client.name}</h2>
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
      <div className="form-actions">
        <button className="secondary-button" onClick={onCorrect} type="button">
          Edit client
        </button>
        <button className="primary-button" onClick={onOpenMatter} type="button">
          Open matter
        </button>
      </div>
    </section>
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
        <div className="report-panel">
          <div className="panel-heading">
            <h2>Matters for {client.clientCode}</h2>
            <span className="row-count">
              {firstRow}–{lastRow} of {matterTotal} matching
            </span>
          </div>
          <table className="rollup-table entries-table">
            <thead>
              <tr>
                <th scope="col">Number</th>
                <th scope="col">Matter name</th>
                <th scope="col">Default billable</th>
                <th scope="col">Active</th>
              </tr>
            </thead>
            <tbody>
              {matterRows.map((row) => {
                const isSelected = row.matterId === selectedMatter?.matterId;
                return (
                  <tr
                    className={isSelected ? "row-selected" : undefined}
                    key={row.matterId}
                  >
                    <td data-label="Number">
                      <button
                        aria-current={isSelected ? "true" : undefined}
                        className="row-select"
                        onClick={() => onSelectMatter(row)}
                        type="button"
                      >
                        {row.matterNumber}
                      </button>
                    </td>
                    <td data-label="Matter name">{row.name}</td>
                    <td data-label="Default billable">
                      {row.isBillableByDefault ? "Yes" : "No"}
                    </td>
                    <td data-label="Active">
                      <span
                        className={
                          row.isActive
                            ? "status-text status-open"
                            : "status-text"
                        }
                      >
                        {formatActiveFlag(row.isActive)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <nav aria-label="Matter pages" className="pagination">
            <div className="page-size">
              <label htmlFor="matter-page-size">Rows per page</label>
              <select
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
            </div>
            <p aria-live="polite" className="page-status">
              Page {matterPage} of {matterPageCount}
            </p>
            <div className="page-actions">
              <button
                className="secondary-button"
                disabled={matterPage === 1}
                onClick={onPreviousMatterPage}
                type="button"
              >
                Previous
              </button>
              <button
                className="secondary-button"
                disabled={matterPage === matterPageCount}
                onClick={onNextMatterPage}
                type="button"
              >
                Next
              </button>
            </div>
          </nav>
        </div>
      )}
      {selectedMatter !== null && (
        <section className="detail-pane" aria-labelledby="matter-detail-title">
          <div className="detail-header">
            <h2 id="matter-detail-title">{selectedMatter.name}</h2>
            <button
              className="secondary-button"
              onClick={onCorrectMatter}
              type="button"
            >
              Edit matter
            </button>
          </div>
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
        </section>
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
      return (
        <div
          aria-label="Loading directory"
          aria-valuetext="Loading"
          className="loading-bar"
          role="progressbar"
        />
      );
    case "empty":
      return (
        <section className="status-panel status-empty" role="status">
          <div>
            <h2>{emptyTitle}</h2>
            <p>{emptyBody}</p>
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
            <h2>{unavailableTitle}</h2>
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
    case "missing-parent":
      return (
        <section className="status-panel status-info" role="status">
          <div>
            <h2>{missingTitle}</h2>
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
