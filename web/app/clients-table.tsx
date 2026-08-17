import {
  formatActiveFlag,
  formatUtcDate,
  partyPageSizes,
  type ClientDto,
  type PartyPageSize,
} from "./parties-api";

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
    <div className="report-panel">
      <div className="panel-heading">
        <h2>Clients</h2>
        <span className="row-count">
          {firstRow}–{lastRow} of {total} matching
        </span>
      </div>
      <table className="rollup-table entries-table">
        <thead>
          <tr>
            <th scope="col">Code</th>
            <th scope="col">Client name</th>
            <th scope="col">Active</th>
            <th scope="col">Registered</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const isSelected = row.clientId === selectedId;
            return (
              <tr
                className={isSelected ? "row-selected" : undefined}
                key={row.clientId}
              >
                <td data-label="Code">
                  <button
                    aria-current={isSelected ? "true" : undefined}
                    className="row-select"
                    onClick={() => onSelect(row)}
                    type="button"
                  >
                    {row.clientCode}
                  </button>
                </td>
                <td data-label="Client name">{row.name}</td>
                <td data-label="Active">
                  <span
                    className={
                      row.isActive ? "status-text status-open" : "status-text"
                    }
                  >
                    {formatActiveFlag(row.isActive)}
                  </span>
                </td>
                <td data-label="Registered">{formatUtcDate(row.createdAtUtc)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <nav aria-label="Client pages" className="pagination">
        <div className="page-size">
          <label htmlFor="client-page-size">Rows per page</label>
          <select
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
