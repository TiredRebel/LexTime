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
    <div className="list-panel">
      <div className="panel-heading">
        <h2>Time entries</h2>
        <span className="row-count">
          {firstRow}–{lastRow} of {total} matching
        </span>
      </div>
      <table className="data-table data-table--wide">
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Narrative</th>
            <th scope="col">Timekeeper</th>
            <th scope="col">Matter</th>
            <th scope="col">Duration</th>
            <th scope="col">Billable</th>
            <th scope="col">Rate</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const isSelected = row.timeEntryId === selectedId;
            return (
              <tr
                className={isSelected ? "row-selected" : undefined}
                key={row.timeEntryId}
              >
                <td data-label="Date">{row.workDate}</td>
                <td data-label="Narrative">
                  <button
                    aria-current={isSelected ? "true" : undefined}
                    className="row-select"
                    onClick={() => onSelect(row)}
                    type="button"
                  >
                    {row.narrative}
                  </button>
                </td>
                <td data-label="Timekeeper">
                  {timekeeperNames.get(row.userId) ?? `#${row.userId}`}
                </td>
                <td data-label="Matter">
                  {matterNames.get(row.matterId) ?? `#${row.matterId}`}
                </td>
                <td data-label="Duration">
                  {formatDurationHours(row.durationMinutes)} h
                </td>
                <td data-label="Billable">
                  {row.isBillable ? "Billable" : "Not billable"}
                </td>
                <td data-label="Rate">
                  {formatCurrency(row.hourlyRateSnapshot)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <nav aria-label="Time entry pages" className="pagination">
        <div className="page-size">
          <label htmlFor="entry-page-size">Rows per page</label>
          <select
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
        </div>
        <p aria-live="polite" className="page-status">
          Page {page} of {pageCount}
        </p>
        <div className="page-actions">
          <button
            className="secondary-button"
            disabled={page === 1}
            onClick={onPreviousPage}
            type="button"
          >
            Previous
          </button>
          <button
            className="secondary-button"
            disabled={page === pageCount}
            onClick={onNextPage}
            type="button"
          >
            Next
          </button>
        </div>
      </nav>
    </div>
  );
}
