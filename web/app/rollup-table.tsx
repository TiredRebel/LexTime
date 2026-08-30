import { Card, td, th } from "@/components/kit";
import { cn } from "@/lib/utils";

import {
  formatCurrency,
  formatHours,
  formatWeek,
  type WeeklyBillableRollupRow,
} from "./reporting";

interface RollupTableProps {
  readonly onNextPage: () => void;
  readonly onPageSizeChange: (pageSize: PageSize) => void;
  readonly onPreviousPage: () => void;
  readonly page: number;
  readonly pageCount: number;
  readonly pageSize: PageSize;
  readonly rows: readonly WeeklyBillableRollupRow[];
  readonly totalRows: number;
}

export type PageSize = 20 | 50 | 100;

const pageSizes: readonly PageSize[] = [20, 50, 100];

function formatDelta(value: number | null): {
  readonly className: string;
  readonly text: string;
} {
  if (value === null) {
    return {
      className: "text-slate-400",
      text: "—",
    };
  }

  if (value > 0) {
    return {
      className: "text-brand font-semibold",
      text: `↑ +${formatHours(value)} h`,
    };
  }

  if (value < 0) {
    return {
      className: "text-red-600 font-semibold",
      text: `↓ ${formatHours(value)} h`,
    };
  }

  return {
    className: "text-slate-400",
    text: "0.0 h",
  };
}

export function RollupTable({
  onNextPage,
  onPageSizeChange,
  onPreviousPage,
  page,
  pageCount,
  pageSize,
  rows,
  totalRows,
}: RollupTableProps): React.JSX.Element {
  const firstRow = totalRows === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastRow = Math.min(page * pageSize, totalRows);
  const maxBillable = Math.max(1, ...rows.map((row) => row.billableHours));

  function changePageSize(value: string): void {
    const parsedValue = Number(value);
    if (parsedValue === 20 || parsedValue === 50 || parsedValue === 100) {
      onPageSizeChange(parsedValue);
    }
  }

  return (
    <Card
      meta={`${firstRow}–${lastRow} of ${totalRows} rows`}
      title="Rollup by client"
    >
      <table className="w-full text-left">
        <thead>
          <tr className="border-b border-slate-200">
            <th className={th}>Client</th>
            <th className={th}>Week</th>
            <th className={cn(th, "text-right")}>Billable</th>
            <th className={cn(th, "text-right")}>Non-bill.</th>
            <th className={cn(th, "text-right")}>Amount</th>
            <th className={cn(th, "text-right")}>Cumul.</th>
            <th className={cn(th, "text-right")}>Delta</th>
            <th className={cn(th, "text-right")}>Rank</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 tabular-nums">
          {rows.map((row) => {
            const delta = formatDelta(row.hoursDeltaVsPriorWeek);
            return (
              <tr
                key={`${row.clientId}-${row.weekStartDate}`}
                className="transition-colors duration-150 hover:bg-slate-50"
              >
                <td className={td}>
                  <span className="font-semibold">{row.clientName}</span>{" "}
                  <span className="text-[11px] text-slate-400">{row.clientCode}</span>
                </td>
                <td className={cn(td, "text-slate-600")}>{formatWeek(row)}</td>
                <td className={cn(td, "relative w-[170px] text-right font-semibold")}>
                  <span
                    className="absolute inset-y-1 right-3 rounded-sm bg-brand/12"
                    style={{
                      width: `${Math.max(8, (row.billableHours / maxBillable) * 100)}%`,
                    }}
                  />
                  <span className="relative">{formatHours(row.billableHours)} h</span>
                </td>
                <td className={cn(td, "text-right text-slate-500")}>
                  {formatHours(row.nonBillableHours)} h
                </td>
                <td className={cn(td, "text-right font-semibold")}>
                  {formatCurrency(row.billableAmount)}
                </td>
                <td className={cn(td, "text-right text-slate-500")}>
                  {formatHours(row.cumulativeBillableHours)} h
                </td>
                <td className={cn(td, "text-right", delta.className)}>
                  {delta.text}
                </td>
                <td className={cn(td, "text-right text-slate-500")}>
                  {String(row.clientRankInWeek).padStart(2, "0")}
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
            id="page-size"
            onChange={(event) => changePageSize(event.target.value)}
            value={pageSize}
          >
            {pageSizes.map((size) => (
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
            onClick={onPreviousPage}
            type="button"
          >
            Previous
          </button>
          <button
            className="rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] disabled:opacity-50"
            disabled={page === pageCount}
            onClick={onNextPage}
            type="button"
          >
            Next
          </button>
        </div>
      </div>
    </Card>
  );
}
