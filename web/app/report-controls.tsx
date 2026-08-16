import type { FormEvent } from "react";

export interface ClientOption {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

interface ReportControlsProps {
  readonly clients: readonly ClientOption[];
  readonly from: string;
  readonly onClientChange: (clientId: string) => void;
  readonly onFromChange: (value: string) => void;
  readonly onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  readonly onToChange: (value: string) => void;
  readonly selectedClientId: string;
  readonly to: string;
  readonly validationMessage: string | null;
}

export function ReportControls({
  clients,
  from,
  onClientChange,
  onFromChange,
  onSubmit,
  onToChange,
  selectedClientId,
  to,
  validationMessage,
}: ReportControlsProps): React.JSX.Element {
  return (
    <form className="report-controls" onSubmit={onSubmit}>
      <div className="field">
        <label htmlFor="report-from">From</label>
        <input
          aria-describedby={validationMessage === null ? undefined : "range-error"}
          aria-invalid={validationMessage !== null}
          id="report-from"
          onChange={(event) => onFromChange(event.target.value)}
          required
          type="date"
          value={from}
        />
      </div>
      <div className="field">
        <label htmlFor="report-to">To</label>
        <input
          aria-describedby={validationMessage === null ? undefined : "range-error"}
          aria-invalid={validationMessage !== null}
          id="report-to"
          onChange={(event) => onToChange(event.target.value)}
          required
          type="date"
          value={to}
        />
      </div>
      <div className="field">
        <label htmlFor="client-filter">Client</label>
        <select
          id="client-filter"
          onChange={(event) => onClientChange(event.target.value)}
          value={selectedClientId}
        >
          <option value="">All clients</option>
          {clients.map((client) => (
            <option key={client.id} value={client.id}>
              {client.code} · {client.name}
            </option>
          ))}
        </select>
      </div>
      <button className="primary-button" type="submit">
        Apply range
      </button>
      {validationMessage !== null && (
        <p className="field-error" id="range-error" role="alert">
          {validationMessage}
        </p>
      )}
    </form>
  );
}
