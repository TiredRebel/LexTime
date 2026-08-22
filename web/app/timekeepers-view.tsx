import { useEffect, useState } from "react";

import {
  formatActiveFlag,
  getTimekeeper,
  listTimekeepers,
  partyPageSizes,
  PartyRequestError,
  type PartyPageSize,
  type TimekeeperDto,
} from "./parties-api";
import {
  stateFromTimekeeperPage,
  type TimekeepersState,
} from "./parties-state";
import { formatCurrency } from "./reporting";

interface TimekeepersViewProps {
  readonly onUnauthorized: (message: string) => void;
  readonly token: string;
}

export function TimekeepersView({
  onUnauthorized,
  token,
}: TimekeepersViewProps): React.JSX.Element {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PartyPageSize>(20);
  const [state, setState] = useState<TimekeepersState>({ kind: "idle" });
  const [selected, setSelected] = useState<TimekeeperDto | null>(null);

  const listingPage =
    state.kind === "ready" || state.kind === "empty" ? state.page : null;
  const rows = listingPage?.items ?? [];
  const total = listingPage?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  useEffect(() => {
    void loadListing(token, 1, 20);
  }, [token]);

  async function loadListing(
    activeToken: string,
    requestedPage: number,
    requestedPageSize: PartyPageSize,
  ): Promise<void> {
    setState({ kind: "loading" });

    try {
      const pageResult = await listTimekeepers(
        {
          skip: (requestedPage - 1) * requestedPageSize,
          take: requestedPageSize,
        },
        activeToken,
      );
      setState(stateFromTimekeeperPage(pageResult));
    } catch (error) {
      if (
        error instanceof PartyRequestError &&
        error.kind === "unauthorized"
      ) {
        onUnauthorized(error.message);
        setState({ kind: "unauthenticated", message: error.message });
        return;
      }

      setState({
        kind: "unavailable",
        message:
          "Timekeepers could not be loaded. Check that the API is running, then retry.",
      });
    }
  }

  async function selectTimekeeper(timekeeper: TimekeeperDto): Promise<void> {
    setSelected(timekeeper);

    try {
      const fresh = await getTimekeeper(timekeeper.userId, token);
      setSelected(fresh);
    } catch (error) {
      if (
        error instanceof PartyRequestError &&
        error.kind === "unauthorized"
      ) {
        onUnauthorized(error.message);
        setState({ kind: "unauthenticated", message: error.message });
        return;
      }

      if (error instanceof PartyRequestError && error.kind === "missing") {
        setSelected(null);
        setState({ kind: "missing", message: error.message });
      }
    }
  }

  function changePageSize(nextPageSize: PartyPageSize): void {
    setPageSize(nextPageSize);
    setPage(1);
    void loadListing(token, 1, nextPageSize);
  }

  const firstRow = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastRow = Math.min(page * pageSize, total);

  return (
    <>
      <header className="report-header">
        <div>
          <p className="eyebrow">Directories</p>
          <h1 className="report-title">Timekeepers</h1>
          <p className="period-label">Seeded roster. Read-only.</p>
        </div>
      </header>

      <TimekeepersStatus
        onRetry={() => void loadListing(token, page, pageSize)}
        state={state}
      />

      <div className="entries-workspace">
        {state.kind === "ready" && (
          <div className="list-panel">
            <div className="panel-heading">
              <h2>Timekeepers</h2>
              <span className="row-count">
                {firstRow}–{lastRow} of {total} matching
              </span>
            </div>
            <table className="data-table data-table--wide">
              <thead>
                <tr>
                  <th scope="col">Timekeeper</th>
                  <th scope="col">Email</th>
                  <th scope="col">Current rate</th>
                  <th scope="col">Active</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const isSelected = row.userId === selected?.userId;
                  return (
                    <tr
                      className={isSelected ? "row-selected" : undefined}
                      key={row.userId}
                    >
                      <td data-label="Timekeeper">
                        <button
                          aria-current={isSelected ? "true" : undefined}
                          className="row-select"
                          onClick={() => void selectTimekeeper(row)}
                          type="button"
                        >
                          {row.fullName}
                        </button>
                      </td>
                      <td data-label="Email">{row.email}</td>
                      <td data-label="Current rate">
                        {formatCurrency(row.defaultHourlyRate)}
                      </td>
                      <td data-label="Active">
                        <span
                          className={
                            row.isActive
                              ? "status-text status-active"
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
            <nav aria-label="Timekeeper pages" className="pagination">
              <div className="page-size">
                <label htmlFor="timekeeper-page-size">Rows per page</label>
                <select
                  id="timekeeper-page-size"
                  onChange={(event) => {
                    const parsedValue = Number(event.target.value);
                    if (
                      parsedValue === 20 ||
                      parsedValue === 50 ||
                      parsedValue === 100
                    ) {
                      changePageSize(parsedValue);
                    }
                  }}
                  value={pageSize}
                >
                  {partyPageSizes.map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
              </div>
              <p aria-live="polite" className="page-status">
                Page {page} of {pageCount}
              </p>
              <div className="page-actions">
                <button
                  className="secondary-button"
                  disabled={page === 1}
                  onClick={() => {
                    const nextPage = Math.max(page - 1, 1);
                    setPage(nextPage);
                    void loadListing(token, nextPage, pageSize);
                  }}
                  type="button"
                >
                  Previous
                </button>
                <button
                  className="secondary-button"
                  disabled={page === pageCount}
                  onClick={() => {
                    const nextPage = Math.min(page + 1, pageCount);
                    setPage(nextPage);
                    void loadListing(token, nextPage, pageSize);
                  }}
                  type="button"
                >
                  Next
                </button>
              </div>
            </nav>
          </div>
        )}

        {selected !== null && (
          <section className="detail-pane" aria-labelledby="timekeeper-detail-title">
            <div className="detail-header">
              <h2 id="timekeeper-detail-title">{selected.fullName}</h2>
              <button
                aria-label="Close details"
                className="secondary-button"
                onClick={() => setSelected(null)}
                type="button"
              >
                Close
              </button>
            </div>
            <p className="readonly-banner" role="note">
              Read-only profile. Timekeepers are seeded and cannot be created
              or edited here.
            </p>
            <dl className="detail-list">
              <div>
                <dt>Email</dt>
                <dd>{selected.email}</dd>
              </div>
              <div>
                <dt>Current rate</dt>
                <dd>{formatCurrency(selected.defaultHourlyRate)}</dd>
              </div>
              <div>
                <dt>Active</dt>
                <dd>{formatActiveFlag(selected.isActive)}</dd>
              </div>
            </dl>
          </section>
        )}
      </div>
    </>
  );
}

interface TimekeepersStatusProps {
  readonly onRetry: () => void;
  readonly state: TimekeepersState;
}

function TimekeepersStatus({
  onRetry,
  state,
}: TimekeepersStatusProps): React.JSX.Element | null {
  switch (state.kind) {
    case "idle":
    case "ready":
    case "missing-parent":
      return null;
    case "loading":
      return (
        <div
          aria-label="Loading timekeepers"
          aria-valuetext="Loading"
          className="loading-bar"
          role="progressbar"
        />
      );
    case "empty":
      return (
        <section className="status-panel status-empty" role="status">
          <div>
            <h2>No timekeepers</h2>
            <p>
              The listing completed successfully, but the roster is empty.
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
            <h2>Timekeepers unavailable</h2>
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
            <h2>Timekeeper not found</h2>
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
