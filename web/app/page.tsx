"use client";

import { type FormEvent, useEffect, useMemo, useState } from "react";

import { AppShell, type ShellDestination } from "@/components/app-shell";
import { PageHeader, StatStrip } from "@/components/kit";

import {
  ClientFilterEmptyStatus,
  DevelopmentTokenPrompt,
  ReportStatus,
} from "./dashboard-status";
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

type ShellDestinationAlias = ShellDestination;

function destinationFromHash(): ShellDestinationAlias {
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
  const [destination, setDestination] = useState<ShellDestinationAlias>("reports");
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

  return (
    <AppShell
      destination={destination}
      navOpen={navOpen}
      onNavClose={() => setNavOpen(false)}
      onNavToggle={() => setNavOpen((open) => !open)}
      onSignOut={signOut}
    >
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
        <PageHeader
          subtitle={`${displayedFrom || "start required"} – ${displayedTo || "end required"}`}
          title="Weekly billable rollup"
        />

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
            <StatStrip
              items={[
                {
                  label: "Billable hrs",
                  value: formatHours(totals.billableHours),
                },
                {
                  label: "Amount",
                  value: formatCurrency(totals.billableAmount),
                },
                {
                  label: "Clients shown",
                  value: String(totals.clientCount),
                },
              ]}
              cols={3}
            />
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
    </AppShell>
  );
}
