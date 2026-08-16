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
      className: "delta-none",
      text: "No comparison",
    };
  }

  if (value > 0) {
    return {
      className: "delta-positive",
      text: `↑ +${formatHours(value)} h`,
    };
  }

  if (value < 0) {
    return {
      className: "delta-negative",
      text: `↓ ${formatHours(value)} h`,
    };
  }

  return {
    className: "delta-none",
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

  function changePageSize(value: string): void {
    const parsedValue = Number(value);
    if (parsedValue === 20 || parsedValue === 50 || parsedValue === 100) {
      onPageSizeChange(parsedValue);
    }
  }

  return (
    <div className="report-panel">
      <div className="panel-heading">
        <h2>Weekly rollup by client</h2>
        <span className="row-count">
          {firstRow}–{lastRow} of {totalRows} rows
        </span>
      </div>
      <table className="rollup-table">
        <thead>
          <tr>
            <th scope="col">Client</th>
            <th scope="col">Week</th>
            <th scope="col">Billable</th>
            <th scope="col">Non-billable</th>
            <th scope="col">Amount</th>
            <th scope="col">Cumulative</th>
            <th scope="col">Delta</th>
            <th scope="col">Rank</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const delta = formatDelta(row.hoursDeltaVsPriorWeek);
            return (
              <tr key={`${row.clientId}-${row.weekStartDate}`}>
                <td data-label="Client">
                  <span className="client-name">{row.clientName}</span>
                  <span className="client-code">{row.clientCode}</span>
                </td>
                <td data-label="Week">{formatWeek(row)}</td>
                <td data-label="Billable">{formatHours(row.billableHours)} h</td>
                <td data-label="Non-billable">
                  {formatHours(row.nonBillableHours)} h
                </td>
                <td data-label="Amount">
                  {formatCurrency(row.billableAmount)}
                </td>
                <td data-label="Cumulative">
                  {formatHours(row.cumulativeBillableHours)} h
                </td>
                <td className={delta.className} data-label="Delta">
                  {delta.text}
                </td>
                <td data-label="Rank">
                  <span
                    aria-label={`Rank ${row.clientRankInWeek}`}
                    className="rank-badge"
                  >
                    {row.clientRankInWeek}
                  </span>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <nav aria-label="Rollup table pages" className="pagination">
        <div className="page-size">
          <label htmlFor="page-size">Rows per page</label>
          <select
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
