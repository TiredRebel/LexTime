import { Card, td, th } from "@/components/kit";
import { cn } from "@/lib/utils";

import { formatActiveFlag, formatUtcDate, partyPageSizes, type ClientDto, type PartyPageSize } from "./parties-api";

interface ClientsTableProps {
  readonly onNextPage: () => void;
  readonly onPageSizeChange: (pageSize: PartyPageSize) => void;
  readonly onPreviousPage: () => void;
  readonly onSelect: (client: ClientDto) => void;
  readonly page: number;
  readonly pageCount: number;
  readonly pageSize: PartyPageSize;
  readonly rows: readonly ClientDto[];
  readonly selectedId: number | null;
  readonly total: number;
}

export function ClientsTable({
  onNextPage,
  onPageSizeChange,
  onPreviousPage,
  onSelect,
  page,
  pageCount,
  pageSize,
  rows,
  selectedId,
  total,
}: ClientsTableProps): React.JSX.Element {
  const firstRow = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastRow = Math.min(page * pageSize, total);

  function changePageSize(value: string): void {
    const parsedValue = Number(value);
    if (parsedValue === 20 || parsedValue === 50 || parsedValue === 100) {
      onPageSizeChange(parsedValue);
    }
  }

  return (
    <Card meta={`${firstRow}–${lastRow} of ${total} matching`} title="Client directory">
      <table className="w-full text-left">
        <thead>
          <tr className="border-b border-slate-200">
            <th className={th}>Code</th>
            <th className={th}>Client name</th>
            <th className={cn(th, "text-center")}>Active</th>
            <th className={cn(th, "text-right")}>Registered</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 tabular-nums">
          {rows.map((row) => {
            const isSelected = row.clientId === selectedId;
            return (
              <tr
                key={row.clientId}
                className={cn(
                  "cursor-pointer transition-colors duration-150 hover:bg-slate-50",
                  isSelected && "bg-brand/[0.07] hover:bg-brand/10",
                )}
                onClick={() => onSelect(row)}
              >
                <td className={cn(td, "font-semibold", isSelected ? "text-brand" : "text-ink")}>
                  {row.clientCode}
                </td>
                <td className={cn(td, row.isActive ? "font-medium" : "text-slate-400")}>
                  {row.name}
                </td>
                <td className={cn(td, "text-center text-[12px] font-semibold", row.isActive ? "text-brand" : "text-slate-400")}>
                  {formatActiveFlag(row.isActive)}
                </td>
                <td className={cn(td, "text-right text-slate-500")}>
                  {formatUtcDate(row.createdAtUtc)}
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
            id="client-page-size"
            onChange={(event) => changePageSize(event.target.value)}
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
