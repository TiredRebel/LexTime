import { Card, td, th } from "@/components/kit";
import { cn } from "@/lib/utils";

import { formatCurrency } from "./reporting";
import {
  formatDurationHours,
  type TimeEntryDto,
  type TimeEntryPageSize,
} from "./time-entries-api";

export const timeEntryPageSizes: readonly TimeEntryPageSize[] = [20, 50, 100];

interface TimeEntriesTableProps {
  readonly matterNames: ReadonlyMap<number, string>;
  readonly onNextPage: () => void;
  readonly onPageSizeChange: (pageSize: TimeEntryPageSize) => void;
  readonly onPreviousPage: () => void;
  readonly onSelect: (entry: TimeEntryDto) => void;
  readonly page: number;
  readonly pageCount: number;
  readonly pageSize: TimeEntryPageSize;
  readonly rows: readonly TimeEntryDto[];
  readonly selectedId: number | null;
  readonly timekeeperNames: ReadonlyMap<number, string>;
  readonly total: number;
}

export function TimeEntriesTable({
  matterNames,
  onNextPage,
  onPageSizeChange,
  onPreviousPage,
  onSelect,
  page,
  pageCount,
  pageSize,
  rows,
  selectedId,
  timekeeperNames,
  total,
}: TimeEntriesTableProps): React.JSX.Element {
  const firstRow = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastRow = Math.min(page * pageSize, total);

  function changePageSize(value: string): void {
    const parsedValue = Number(value);
    if (parsedValue === 20 || parsedValue === 50 || parsedValue === 100) {
      onPageSizeChange(parsedValue);
    }
  }

  return (
    <Card meta={`${firstRow}–${lastRow} of ${total} matching`} title="Time entries">
      <table className="w-full text-left">
        <thead>
          <tr className="border-b border-slate-200">
            <th className={th}>Date</th>
            <th className={th}>Narrative</th>
            <th className={th}>Timekeeper</th>
            <th className={th}>Matter</th>
            <th className={cn(th, "text-right")}>Duration</th>
            <th className={cn(th, "text-center")}>Billable</th>
            <th className={cn(th, "text-right")}>Rate</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 tabular-nums">
          {rows.map((row) => {
            const isSelected = row.timeEntryId === selectedId;
            return (
              <tr
                key={row.timeEntryId}
                className={cn(
                  "cursor-pointer transition-colors duration-150 hover:bg-slate-50",
                  isSelected && "bg-brand/[0.07] hover:bg-brand/10",
                )}
                onClick={() => onSelect(row)}
              >
                <td className={cn(td, "text-slate-600")}>{row.workDate}</td>
                <td className={cn(td, "font-medium")}>{row.narrative}</td>
                <td className={td}>
                  {timekeeperNames.get(row.userId) ?? `#${row.userId}`}
                </td>
                <td className={cn(td, "text-slate-600")}>
                  {matterNames.get(row.matterId) ?? `#${row.matterId}`}
                </td>
                <td className={cn(td, "text-right font-semibold")}>
                  {formatDurationHours(row.durationMinutes)} h
                </td>
                <td className={cn(td, "text-center text-[12px] font-semibold", row.isBillable ? "text-brand" : "text-slate-400")}>
                  {row.isBillable ? "Billable" : "Non-bill."}
                </td>
                <td className={cn(td, "text-right")}>
                  {formatCurrency(row.hourlyRateSnapshot)}
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
            id="entry-page-size"
            onChange={(event) => changePageSize(event.target.value)}
            value={pageSize}
          >
            {timeEntryPageSizes.map((size) => (
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
