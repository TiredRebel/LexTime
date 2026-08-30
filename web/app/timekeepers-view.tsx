import { useEffect, useState } from "react";

import {
  AlertBanner,
  btn,
  Card,
  LoadingBar,
  PageHeader,
  td,
  th,
} from "@/components/kit";
import { cn } from "@/lib/utils";

import { DetailPanel } from "./detail-form";
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
      <PageHeader subtitle="Seeded roster. Read-only." title="Timekeepers" />

      <TimekeepersStatus
        onRetry={() => void loadListing(token, page, pageSize)}
        state={state}
      />

      <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
        {state.kind === "ready" && (
          <Card
            meta={`${firstRow}–${lastRow} of ${total} matching`}
            title="Timekeeper directory"
          >
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className={th}>Timekeeper</th>
                  <th className={th}>Email</th>
                  <th className={cn(th, "text-right")}>Current rate</th>
                  <th className={cn(th, "text-center")}>Active</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 tabular-nums">
                {rows.map((row) => {
                  const isSelected = row.userId === selected?.userId;
                  return (
                    <tr
                      key={row.userId}
                      className={cn(
                        "cursor-pointer transition-colors duration-150 hover:bg-slate-50",
                        isSelected && "bg-brand/[0.07]",
                      )}
                      onClick={() => void selectTimekeeper(row)}
                    >
                      <td className={cn(td, "font-semibold", isSelected ? "text-brand" : "")}>
                        {row.fullName}
                      </td>
                      <td className={cn(td, "text-slate-600")}>{row.email}</td>
                      <td className={cn(td, "text-right font-semibold")}>
                        {formatCurrency(row.defaultHourlyRate)}
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
              </label>
              <span aria-live="polite" className="text-[11px] tabular-nums text-slate-500">
                Page {page} of {pageCount}
              </span>
              <div className="flex gap-2">
                <button
                  className="rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] disabled:opacity-50"
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
                  className="rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] disabled:opacity-50"
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
            </div>
          </Card>
        )}

        {selected !== null && (
          <DetailPanel
            headerAction={
              <button
                aria-label="Close details"
                className="rounded border border-white/25 px-2 py-1 text-[11px] font-medium text-white/80 transition-colors hover:bg-white/10"
                onClick={() => setSelected(null)}
                type="button"
              >
                Close
              </button>
            }
            title={selected.fullName}
            titleId="timekeeper-detail-title"
          >
            <p className="rounded-md border-l-[3px] border-brand bg-brand/5 px-4 py-3 text-[13px] text-slate-700" role="note">
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
          </DetailPanel>
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
      return <LoadingBar />;
    case "empty":
      return (
        <AlertBanner title="No timekeepers" variant="empty">
          The listing completed successfully, but the roster is empty.
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
          <AlertBanner title="Timekeepers unavailable" variant="error">
            {state.message}
          </AlertBanner>
          <button className={btn.ghost} onClick={onRetry} type="button">
            Try again
          </button>
        </div>
      );
    case "missing":
      return (
        <AlertBanner title="Timekeeper not found" variant="info">
          {state.message}
        </AlertBanner>
      );
    default: {
      const unhandledState: never = state;
      return unhandledState;
    }
  }
}
