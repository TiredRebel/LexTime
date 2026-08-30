import type { FormEvent } from "react";

import { btn, field } from "@/components/kit";
import { cn } from "@/lib/utils";

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
    <form className="flex flex-wrap items-end gap-2" onSubmit={onSubmit}>
      <label className="block">
        <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">
          From
        </span>
        <input
          aria-describedby={validationMessage === null ? undefined : "range-error"}
          aria-invalid={validationMessage !== null}
          className={cn(field, "w-auto", validationMessage !== null && "border-red-500")}
          id="report-from"
          onChange={(event) => onFromChange(event.target.value)}
          required
          type="date"
          value={from}
        />
      </label>
      <label className="block">
        <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">
          To
        </span>
        <input
          aria-describedby={validationMessage === null ? undefined : "range-error"}
          aria-invalid={validationMessage !== null}
          className={cn(field, "w-auto", validationMessage !== null && "border-red-500")}
          id="report-to"
          onChange={(event) => onToChange(event.target.value)}
          required
          type="date"
          value={to}
        />
      </label>
      <label className="block">
        <span className="mb-1 block text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">
          Client
        </span>
        <select
          className={cn(field, "w-auto min-w-[10rem]")}
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
      </label>
      <button className={btn.primary} type="submit">
        Apply
      </button>
      {validationMessage !== null && (
        <p className="w-full text-[12px] font-medium text-red-600" id="range-error" role="alert">
          {validationMessage}
        </p>
      )}
    </form>
  );
}
