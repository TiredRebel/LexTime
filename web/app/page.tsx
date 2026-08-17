"use client";

import { type FormEvent, useEffect, useMemo, useState } from "react";

import {
  type DashboardState,
  stateFromResponse,
} from "./dashboard-state";
import {
  type ClientOption,
  ReportControls,
} from "./report-controls";
import {
  fetchWeeklyRollup,
  formatCurrency,
  formatHours,
  RollupRequestError,
} from "./reporting";
import { type PageSize, RollupTable } from "./rollup-table";
import {
  ClientFilterEmptyStatus,
  DevelopmentTokenPrompt,
  ReportStatus,
} from "./dashboard-status";
import { TimeEntriesView } from "./time-entries-view";
import { ClientsView } from "./clients-view";
import { TimekeepersView } from "./timekeepers-view";
import {
  clearDevelopmentToken,
  readDevelopmentToken,
  saveDevelopmentToken,
} from "./token-session";

const initialFrom = "2026-06-18";
const initialTo = "2026-08-13";

type ShellDestination =
  | "reports"
  | "time-entries"
  | "clients"
  | "timekeepers";

function destinationFromHash(): ShellDestination {
  switch (window.location.hash) {
    case "#time-entries":
      return "time-entries";
    case "#clients":
      return "clients";
    case "#timekeepers":
      return "timekeepers";
    default:
      return "reports";
  }
}

export default function DashboardPage(): React.JSX.Element {
  const [from, setFrom] = useState(initialFrom);
  const [to, setTo] = useState(initialTo);
  const [token, setToken] = useState<string | null>(null);
  const [tokenInput, setTokenInput] = useState("");
  const [selectedClientId, setSelectedClientId] = useState("");
  const [clients, setClients] = useState<readonly ClientOption[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(20);
  const [state, setState] = useState<DashboardState>({ kind: "idle" });
  const [sessionMessage, setSessionMessage] = useState<string | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(
    null,
  );
  const [destination, setDestination] = useState<ShellDestination>("reports");
  const [navOpen, setNavOpen] = useState(false);

  async function loadReport(
    activeToken: string,
    requestedFrom: string,
    requestedTo: string,
  ): Promise<void> {
    setState({ kind: "loading" });

    try {
      const nextResponse = await fetchWeeklyRollup(
        requestedFrom,
        requestedTo,
        activeToken,
      );
      if (nextResponse.rows.length > 0) {
        const clientsById = new Map<number, ClientOption>();
        for (const row of nextResponse.rows) {
          clientsById.set(row.clientId, {
            code: row.clientCode,
            id: row.clientId,
            name: row.clientName,
          });
        }

        setClients(
          [...clientsById.values()].sort((left, right) =>
            left.name.localeCompare(right.name),
          ),
        );
      }
      setState(stateFromResponse(nextResponse));
    } catch (error) {
      if (error instanceof RollupRequestError && error.kind === "unauthorized") {
        clearDevelopmentToken();
        setToken(null);
        setSessionMessage(error.message);
        setState({ kind: "unauthenticated", message: error.message });
      } else {
        setState({
          kind: "unavailable",
          message:
            "No report data was changed. Check that the API is running, then retry with the same controls.",
        });
      }
    }
  }

  useEffect(() => {
    const storedToken = readDevelopmentToken();
    if (storedToken === null) {
      return;
    }

    setToken(storedToken);
    void loadReport(storedToken, initialFrom, initialTo);
  }, []);

  useEffect(() => {
    function syncDestination(): void {
      setDestination(destinationFromHash());
      setNavOpen(false);
    }

    syncDestination();
    window.addEventListener("hashchange", syncDestination);
    return () => window.removeEventListener("hashchange", syncDestination);
  }, []);

  useEffect(() => {
    if (!navOpen) {
      return;
    }

    function closeOnEscape(event: KeyboardEvent): void {
      if (event.key === "Escape") {
        setNavOpen(false);
      }
    }

    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [navOpen]);

  const response =
    state.kind === "ready" || state.kind === "empty"
      ? state.response
      : null;

  const visibleRows = useMemo(() => {
    if (selectedClientId.length === 0) {
      return response?.rows ?? [];
    }

    const clientId = Number(selectedClientId);
    return (response?.rows ?? []).filter((row) => row.clientId === clientId);
  }, [response, selectedClientId]);

  const pageCount = Math.max(1, Math.ceil(visibleRows.length / pageSize));
  const pagedRows = useMemo(() => {
    const firstRow = (page - 1) * pageSize;
    return visibleRows.slice(firstRow, firstRow + pageSize);
  }, [page, pageSize, visibleRows]);

  const totals = useMemo(() => {
    const clientIds = new Set<number>();
    let billableHours = 0;
    let billableAmount = 0;

    for (const row of visibleRows) {
      clientIds.add(row.clientId);
      billableHours += row.billableHours;
      billableAmount += row.billableAmount;
    }

    return {
      billableAmount,
      billableHours,
      clientCount: clientIds.size,
    };
  }, [visibleRows]);

  function submitToken(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const normalizedToken = tokenInput.trim();
    if (normalizedToken.length === 0) {
      setSessionMessage(
        "Paste the development token printed by the setup command.",
      );
      return;
    }

    saveDevelopmentToken(normalizedToken);
    setToken(normalizedToken);
    setTokenInput("");
    setSessionMessage(null);
    setPage(1);
    void loadReport(normalizedToken, from, to);
  }

  function applyRange(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (token === null) {
      setSessionMessage("Paste a development token before loading the report.");
      return;
    }

    if (from.length === 0 || to.length === 0 || from > to) {
      const nextMessage =
        "Choose a complete range whose start is not after its end.";
      setValidationMessage(nextMessage);
      setState({ kind: "blocked-range", message: nextMessage });
      return;
    }

    setValidationMessage(null);
    setPage(1);
    void loadReport(token, from, to);
  }

  function changeClient(clientId: string): void {
    setSelectedClientId(clientId);
    setPage(1);
  }

  function changeFrom(value: string): void {
    setFrom(value);
    setPage(1);
  }

  function changeTo(value: string): void {
    setTo(value);
    setPage(1);
  }

  function changePageSize(nextPageSize: PageSize): void {
    setPageSize(nextPageSize);
    setPage(1);
  }

  function signOut(): void {
    clearDevelopmentToken();
    setToken(null);
    setState({ kind: "idle" });
    setSessionMessage(null);
  }

  if (token === null) {
    return (
      <DevelopmentTokenPrompt
        message={sessionMessage}
        onSubmit={submitToken}
        onTokenChange={setTokenInput}
        token={tokenInput}
      />
    );
  }

  const displayedFrom = response?.from ?? from;
  const displayedTo = response?.to ?? to;
  const periodPrefix =
    response === null
      ? state.kind === "loading"
        ? "Loading period"
        : "Selected period (not current)"
      : "Current period";

  return (
    <div className={navOpen ? "app-shell nav-open" : "app-shell"}>
      <header className="shell-topbar">
        <div className="wordmark">LexTime</div>
        <button
          aria-controls="app-sidebar"
          aria-expanded={navOpen}
          className="nav-toggle"
          onClick={() => setNavOpen((open) => !open)}
          type="button"
        >
          {navOpen ? "Close" : "Menu"}
        </button>
      </header>
      <button
        aria-label="Close menu"
        className="nav-backdrop"
        hidden={!navOpen}
        onClick={() => setNavOpen(false)}
        type="button"
      />
      <aside className="sidebar" id="app-sidebar">
        <div className="wordmark">LexTime</div>
        <nav aria-label="Primary" className="side-nav">
          <a
            aria-current={destination === "time-entries" ? "page" : undefined}
            className={
              destination === "time-entries"
                ? "nav-item nav-item-current"
                : "nav-item"
            }
            href="#time-entries"
            onClick={() => setNavOpen(false)}
          >
            <span aria-hidden="true" className="nav-icon">
              T
            </span>
            Time entries
          </a>
          <a
            aria-current={destination === "clients" ? "page" : undefined}
            className={
              destination === "clients"
                ? "nav-item nav-item-current"
                : "nav-item"
            }
            href="#clients"
            onClick={() => setNavOpen(false)}
          >
            <span aria-hidden="true" className="nav-icon">
              C
            </span>
            Clients
          </a>
          <a
            aria-current={destination === "timekeepers" ? "page" : undefined}
            className={
              destination === "timekeepers"
                ? "nav-item nav-item-current"
                : "nav-item"
            }
            href="#timekeepers"
            onClick={() => setNavOpen(false)}
          >
            <span aria-hidden="true" className="nav-icon">
              K
            </span>
            Timekeepers
          </a>
          <a
            aria-current={destination === "reports" ? "page" : undefined}
            className={
              destination === "reports"
                ? "nav-item nav-item-current"
                : "nav-item"
            }
            href="#reports"
            onClick={() => setNavOpen(false)}
          >
            <span aria-hidden="true" className="nav-icon">
              R
            </span>
            Reports
          </a>
        </nav>
        <div className="sidebar-note">
          Thin consumer of the finished API.
          <br />
          No new billing rules.
          <br />
          <button className="secondary-button" onClick={signOut} type="button">
            Clear session
          </button>
        </div>
      </aside>
      <main className="main-content" id="main-content">
        {destination === "time-entries" ? (
          <TimeEntriesView
            onUnauthorized={(message) => {
              clearDevelopmentToken();
              setToken(null);
              setSessionMessage(message);
            }}
            token={token}
          />
        ) : destination === "clients" ? (
          <ClientsView
            onUnauthorized={(message) => {
              clearDevelopmentToken();
              setToken(null);
              setSessionMessage(message);
            }}
            token={token}
          />
        ) : destination === "timekeepers" ? (
          <TimekeepersView
            onUnauthorized={(message) => {
              clearDevelopmentToken();
              setToken(null);
              setSessionMessage(message);
            }}
            token={token}
          />
        ) : (
          <>
        <header className="report-header">
          <div>
            <p className="eyebrow">Reports / Weekly</p>
            <h1 className="report-title">Weekly billable rollup</h1>
            <p className="period-label">
              {periodPrefix}: {displayedFrom || "start required"} —{" "}
              {displayedTo || "end required"}
            </p>
          </div>
        </header>

        <ReportControls
          clients={clients}
          from={from}
          onClientChange={changeClient}
          onFromChange={changeFrom}
          onSubmit={applyRange}
          onToChange={changeTo}
          selectedClientId={selectedClientId}
          to={to}
          validationMessage={validationMessage}
        />

        <ReportStatus
          onRetry={() => void loadReport(token, from, to)}
          state={state}
        />

        {state.kind === "ready" && visibleRows.length === 0 && (
          <ClientFilterEmptyStatus />
        )}

        {state.kind === "ready" && visibleRows.length > 0 && (
          <>
            <section aria-label="Period summary" className="summary-grid">
              <article className="summary-card">
                <span className="summary-label">Billable hours</span>
                <div className="summary-value">
                  {formatHours(totals.billableHours)}
                </div>
              </article>
              <article className="summary-card">
                <span className="summary-label">Billable amount</span>
                <div className="summary-value">
                  {formatCurrency(totals.billableAmount)}
                </div>
              </article>
              <article className="summary-card">
                <span className="summary-label">Clients shown</span>
                <div className="summary-value">{totals.clientCount}</div>
              </article>
            </section>
            <RollupTable
              onNextPage={() =>
                setPage((currentPage) =>
                  Math.min(currentPage + 1, pageCount),
                )
              }
              onPageSizeChange={changePageSize}
              onPreviousPage={() =>
                setPage((currentPage) => Math.max(currentPage - 1, 1))
              }
              page={page}
              pageCount={pageCount}
              pageSize={pageSize}
              rows={pagedRows}
              totalRows={visibleRows.length}
            />
          </>
        )}
          </>
        )}
      </main>
    </div>
  );
}
