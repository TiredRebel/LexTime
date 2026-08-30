import { type FormEvent, useState } from "react";

import { Field, field } from "@/components/kit";

import { DetailForm, type DetailFormMessage } from "./detail-form";
import {
  type MatterDto,
  formatActiveFlag,
} from "./parties-api";

export type MatterFormMode = "open" | "correct";

export interface OpenMatterValues {
  readonly isBillableByDefault: boolean;
  readonly matterNumber: string;
  readonly name: string;
}

export interface CorrectMatterValues {
  readonly isActive: boolean;
  readonly isBillableByDefault: boolean;
  readonly name: string;
}

interface MatterFormProps {
  readonly clientLabel: string;
  readonly conflictField: string | null;
  readonly conflictValue: string | null;
  readonly fieldError: string | null;
  readonly isSubmitting: boolean;
  readonly matter: MatterDto | null;
  readonly mode: MatterFormMode;
  readonly onCancel: () => void;
  readonly onSubmitCorrect: (values: CorrectMatterValues) => void;
  readonly onSubmitOpen: (values: OpenMatterValues) => void;
}

export function MatterForm({
  clientLabel,
  conflictField,
  conflictValue,
  fieldError,
  isSubmitting,
  matter,
  mode,
  onCancel,
  onSubmitCorrect,
  onSubmitOpen,
}: MatterFormProps): React.JSX.Element {
  const [matterNumber, setMatterNumber] = useState("");
  const [name, setName] = useState(matter?.name ?? "");
  const [isBillableByDefault, setIsBillableByDefault] = useState(
    matter?.isBillableByDefault ?? true,
  );
  const [isActive, setIsActive] = useState(matter?.isActive ?? true);
  const [localError, setLocalError] = useState<string | null>(null);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedNumber = matterNumber.trim();

    if (mode === "open") {
      if (trimmedNumber.length === 0 || trimmedName.length === 0) {
        setLocalError("Fill every required field before saving.");
        return;
      }

      setLocalError(null);
      onSubmitOpen({
        isBillableByDefault,
        matterNumber: trimmedNumber,
        name: trimmedName,
      });
      return;
    }

    if (trimmedName.length === 0) {
      setLocalError("Fill every required field before saving.");
      return;
    }

    setLocalError(null);
    onSubmitCorrect({
      isActive,
      isBillableByDefault,
      name: trimmedName,
    });
  }

  const messages: readonly DetailFormMessage[] =
    conflictField === null
      ? []
      : [
          {
            detail:
              conflictValue === null
                ? "This value is already in use."
                : `Conflicting value: ${conflictValue}`,
            rule: conflictField,
          },
        ];

  return (
    <DetailForm
      fieldError={localError ?? fieldError}
      isSubmitting={isSubmitting}
      messages={messages}
      onCancel={onCancel}
      onSubmit={submit}
      submitLabel={mode === "open" ? "Save matter" : "Save changes"}
      title={mode === "open" ? "Open matter" : "Edit matter"}
      titleId="matter-form-title"
    >
      <Field label="Client">
        <p className="flex min-h-[2.25rem] items-center text-[13px] text-ink">
          {clientLabel}
        </p>
      </Field>
      {mode === "open" ? (
        <Field label="Matter number">
          <input
            autoComplete="off"
            className={field}
            id="matter-number"
            onChange={(event) => setMatterNumber(event.target.value)}
            required
            value={matterNumber}
          />
        </Field>
      ) : (
        <Field label="Matter number">
          <p className="flex min-h-[2.25rem] items-center text-[13px] text-ink">
            {matter?.matterNumber}
          </p>
        </Field>
      )}
      <Field label="Matter name">
        <input
          className={field}
          id="matter-name"
          onChange={(event) => setName(event.target.value)}
          required
          value={name}
        />
      </Field>
      <label className="flex items-center gap-2 text-[13px] font-medium text-ink">
        <input
          checked={isBillableByDefault}
          className="size-4 accent-brand"
          id="matter-billable"
          onChange={(event) => setIsBillableByDefault(event.target.checked)}
          type="checkbox"
        />
        Default billable
      </label>
      {mode === "correct" && (
        <label className="flex items-center gap-2 text-[13px] font-medium text-ink">
          <input
            checked={isActive}
            className="size-4 accent-brand"
            id="matter-active"
            onChange={(event) => setIsActive(event.target.checked)}
            type="checkbox"
          />
          {formatActiveFlag(isActive)}
        </label>
      )}
    </DetailForm>
  );
}
