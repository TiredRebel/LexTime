import { type FormEvent, useMemo, useState } from "react";

import { btn, DetailPanel as KitDetailPanel, Field, field } from "@/components/kit";

import { DetailForm, DetailFormMessages, type DetailFormMessage } from "./detail-form";
import {
  type ClientDto,
  type MatterDto,
  partyLabel,
  type TimekeeperDto,
} from "./party-lookups";
import { formatCurrency } from "./reporting";
import {
  formatDurationHours,
  type DomainRuleViolation,
  type TimeEntryDto,
} from "./time-entries-api";

export type TimeEntryFormMode = "record" | "revise" | "delete";

interface TimeEntryFormProps {
  readonly capturedRate: number | null;
  readonly clients: readonly ClientDto[];
  readonly entry: TimeEntryDto | null;
  readonly fieldError: string | null;
  readonly isSubmitting: boolean;
  readonly matters: readonly MatterDto[];
  readonly mode: TimeEntryFormMode;
  readonly onCancel: () => void;
  readonly onConfirmDelete: () => void;
  readonly onPickerClientChange: (clientId: string) => void;
  readonly onSubmitRecord: (values: RecordFormValues) => void;
  readonly onSubmitRevise: (values: ReviseFormValues) => void;
  readonly pickerClientId: string;
  readonly timekeepers: readonly TimekeeperDto[];
  readonly timekeeperName: string;
  readonly violations: readonly DomainRuleViolation[];
}

export interface RecordFormValues {
  readonly durationMinutes: number;
  readonly isBillable: boolean;
  readonly matterId: number;
  readonly narrative: string;
  readonly userId: number;
  readonly workDate: string;
}

export interface ReviseFormValues {
  readonly durationMinutes: number;
  readonly isBillable: boolean;
  readonly matterId: number;
  readonly narrative: string;
  readonly workDate: string;
}

export function todayIsoDate(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
}

function violationMessages(
  violations: readonly DomainRuleViolation[],
): readonly DetailFormMessage[] {
  return violations.map((violation) => ({
    detail: violation.detail,
    id: `${violation.rule}-${violation.offendingValue}`,
    rule: violation.rule,
  }));
}

export function TimeEntryForm({
  capturedRate,
  clients,
  entry,
  fieldError,
  isSubmitting,
  matters,
  mode,
  onCancel,
  onConfirmDelete,
  onPickerClientChange,
  onSubmitRecord,
  onSubmitRevise,
  pickerClientId,
  timekeepers,
  timekeeperName,
  violations,
}: TimeEntryFormProps): React.JSX.Element {
  const [userId, setUserId] = useState(
    entry === null ? "" : String(entry.userId),
  );
  const [matterId, setMatterId] = useState(
    entry === null ? "" : String(entry.matterId),
  );
  const [workDate, setWorkDate] = useState(
    entry === null ? todayIsoDate() : entry.workDate,
  );
  const [durationMinutes, setDurationMinutes] = useState(
    entry === null ? "6" : String(entry.durationMinutes),
  );
  const [isBillable, setIsBillable] = useState(
    entry === null ? true : entry.isBillable,
  );
  const [narrative, setNarrative] = useState(entry?.narrative ?? "");
  const [localError, setLocalError] = useState<string | null>(null);

  const selectedMatter = useMemo(
    () => matters.find((matter) => String(matter.matterId) === matterId),
    [matterId, matters],
  );

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();

    if (mode === "delete") {
      return;
    }

    const parsedDuration = Number(durationMinutes);
    const parsedMatterId = Number(matterId);

    if (
      matterId.length === 0 ||
      workDate.length === 0 ||
      durationMinutes.length === 0 ||
      Number.isNaN(parsedDuration) ||
      narrative.trim().length === 0 ||
      (mode === "record" && userId.length === 0)
    ) {
      setLocalError("Fill every required field before saving.");
      return;
    }

    setLocalError(null);

    if (mode === "record") {
      onSubmitRecord({
        durationMinutes: parsedDuration,
        isBillable,
        matterId: parsedMatterId,
        narrative: narrative.trim(),
        userId: Number(userId),
        workDate,
      });
      return;
    }

    onSubmitRevise({
      durationMinutes: parsedDuration,
      isBillable,
      matterId: parsedMatterId,
      narrative: narrative.trim(),
      workDate,
    });
  }

  if (mode === "delete" && entry !== null) {
    return (
      <KitDetailPanel onClose={onCancel} title="Delete time entry">
        <p className="text-[13px] text-slate-700">
          Remove the {formatDurationHours(entry.durationMinutes)} h entry from{" "}
          {entry.workDate}? This cannot be undone.
        </p>
        <DetailFormMessages
          fieldError={localError ?? fieldError}
          messages={violationMessages(violations)}
        />
        <div className="flex flex-wrap justify-end gap-2 pt-2">
          <button className={btn.ghost} onClick={onCancel} type="button">
            Cancel
          </button>
          <button
            className={btn.danger}
            disabled={isSubmitting}
            onClick={onConfirmDelete}
            type="button"
          >
            Delete entry
          </button>
        </div>
      </KitDetailPanel>
    );
  }

  return (
    <DetailForm
      fieldError={localError ?? fieldError}
      isSubmitting={isSubmitting}
      messages={violationMessages(violations)}
      onCancel={onCancel}
      onSubmit={submit}
      submitLabel={mode === "record" ? "Save entry" : "Save changes"}
      title={mode === "record" ? "Record time" : "Edit entry"}
      titleId="entry-form-title"
    >
      {mode === "revise" && (
        <Field label="Timekeeper">
          <p className="flex min-h-[2.25rem] items-center text-[13px] text-ink">
            {timekeeperName}
          </p>
        </Field>
      )}
      {mode === "record" && (
        <Field label="Timekeeper">
          <select
            className={field}
            id="entry-timekeeper"
            onChange={(event) => setUserId(event.target.value)}
            required
            value={userId}
          >
            <option value="">Select a timekeeper</option>
            {timekeepers.map((timekeeper) => (
              <option key={timekeeper.userId} value={timekeeper.userId}>
                {partyLabel(timekeeper.fullName, timekeeper.isActive)}
              </option>
            ))}
          </select>
        </Field>
      )}
      <Field label="Client">
        <select
          className={field}
          id="entry-client"
          onChange={(event) => {
            onPickerClientChange(event.target.value);
            setMatterId("");
          }}
          value={pickerClientId}
        >
          <option value="">Select a client</option>
          {clients.map((client) => (
            <option key={client.clientId} value={client.clientId}>
              {partyLabel(client.name, client.isActive, client.clientCode)}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Matter">
        <select
          className={field}
          id="entry-matter"
          onChange={(event) => {
            const nextMatterId = event.target.value;
            setMatterId(nextMatterId);
            const nextMatter = matters.find(
              (matter) => String(matter.matterId) === nextMatterId,
            );
            if (nextMatter !== undefined && mode === "record") {
              setIsBillable(nextMatter.isBillableByDefault);
            }
          }}
          required
          value={matterId}
        >
          <option value="">Select a matter</option>
          {matters.map((matter) => (
            <option key={matter.matterId} value={matter.matterId}>
              {partyLabel(matter.name, matter.isActive, matter.matterNumber)}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Work date">
        <input
          className={field}
          id="entry-date"
          onChange={(event) => setWorkDate(event.target.value)}
          required
          type="date"
          value={workDate}
        />
      </Field>
      <Field label="Duration (minutes)">
        <input
          className={field}
          id="entry-duration"
          inputMode="numeric"
          min={1}
          onChange={(event) => setDurationMinutes(event.target.value)}
          required
          type="number"
          value={durationMinutes}
        />
      </Field>
      <label className="flex items-center gap-2 text-[13px] font-medium text-ink">
        <input
          checked={isBillable}
          className="size-4 accent-brand"
          id="entry-billable"
          onChange={(event) => setIsBillable(event.target.checked)}
          type="checkbox"
        />
        Billable
      </label>
      <Field label="Narrative">
        <textarea
          className={field}
          id="entry-narrative"
          onChange={(event) => setNarrative(event.target.value)}
          required
          rows={4}
          value={narrative}
        />
      </Field>
      {capturedRate !== null && (
        <Field label="Captured rate">
          <p className="flex min-h-[2.25rem] items-center text-[13px] text-ink">
            {formatCurrency(capturedRate)}
          </p>
        </Field>
      )}
      {selectedMatter !== undefined && mode === "record" && (
        <p className="text-[12px] text-slate-500">
          Default billable flag comes from the matter and can be changed per
          entry.
        </p>
      )}
    </DetailForm>
  );
}
