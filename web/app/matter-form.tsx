import { type FormEvent, useState } from "react";

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
      <div className="field">
        <span>Client</span>
        <p className="readonly-value">{clientLabel}</p>
      </div>
      {mode === "open" ? (
        <div className="field">
          <label htmlFor="matter-number">Matter number</label>
          <input
            autoComplete="off"
            id="matter-number"
            onChange={(event) => setMatterNumber(event.target.value)}
            required
            value={matterNumber}
          />
        </div>
      ) : (
        <div className="field">
          <span>Matter number</span>
          <p className="readonly-value">{matter?.matterNumber}</p>
        </div>
      )}
      <div className="field">
        <label htmlFor="matter-name">Matter name</label>
        <input
          id="matter-name"
          onChange={(event) => setName(event.target.value)}
          required
          value={name}
        />
      </div>
      <div className="field checkbox-field">
        <label htmlFor="matter-billable">
          <input
            checked={isBillableByDefault}
            id="matter-billable"
            onChange={(event) => setIsBillableByDefault(event.target.checked)}
            type="checkbox"
          />
          Default billable
        </label>
      </div>
      {mode === "correct" && (
        <div className="field checkbox-field">
          <label htmlFor="matter-active">
            <input
              checked={isActive}
              id="matter-active"
              onChange={(event) => setIsActive(event.target.checked)}
              type="checkbox"
            />
            {formatActiveFlag(isActive)}
          </label>
        </div>
      )}
    </DetailForm>
  );
}
