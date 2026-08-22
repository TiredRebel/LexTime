import { type FormEvent, useState } from "react";

import { DetailForm, type DetailFormMessage } from "./detail-form";
import {
  type ClientDto,
  formatActiveFlag,
} from "./parties-api";

export type ClientFormMode = "register" | "correct";

export interface RegisterClientValues {
  readonly clientCode: string;
  readonly name: string;
}

export interface CorrectClientValues {
  readonly isActive: boolean;
  readonly name: string;
}

interface ClientFormProps {
  readonly client: ClientDto | null;
  readonly conflictField: string | null;
  readonly conflictValue: string | null;
  readonly fieldError: string | null;
  readonly isSubmitting: boolean;
  readonly mode: ClientFormMode;
  readonly onCancel: () => void;
  readonly onSubmitCorrect: (values: CorrectClientValues) => void;
  readonly onSubmitRegister: (values: RegisterClientValues) => void;
}

export function ClientForm({
  client,
  conflictField,
  conflictValue,
  fieldError,
  isSubmitting,
  mode,
  onCancel,
  onSubmitCorrect,
  onSubmitRegister,
}: ClientFormProps): React.JSX.Element {
  const [clientCode, setClientCode] = useState("");
  const [name, setName] = useState(client?.name ?? "");
  const [isActive, setIsActive] = useState(client?.isActive ?? true);
  const [localError, setLocalError] = useState<string | null>(null);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedCode = clientCode.trim();

    if (mode === "register") {
      if (trimmedCode.length === 0 || trimmedName.length === 0) {
        setLocalError("Fill every required field before saving.");
        return;
      }

      setLocalError(null);
      onSubmitRegister({ clientCode: trimmedCode, name: trimmedName });
      return;
    }

    if (trimmedName.length === 0) {
      setLocalError("Fill every required field before saving.");
      return;
    }

    setLocalError(null);
    onSubmitCorrect({ isActive, name: trimmedName });
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
      submitLabel={mode === "register" ? "Save client" : "Save changes"}
      title={mode === "register" ? "Add client" : "Edit client"}
      titleId="client-form-title"
    >
      {mode === "register" ? (
        <div className="field">
          <label htmlFor="client-code">Client code</label>
          <input
            autoComplete="off"
            id="client-code"
            onChange={(event) => setClientCode(event.target.value)}
            required
            value={clientCode}
          />
        </div>
      ) : (
        <div className="field">
          <span>Client code</span>
          <p className="readonly-value">{client?.clientCode}</p>
        </div>
      )}
      <div className="field">
        <label htmlFor="client-name">Client name</label>
        <input
          id="client-name"
          onChange={(event) => setName(event.target.value)}
          required
          value={name}
        />
      </div>
      {mode === "correct" && (
        <div className="field checkbox-field">
          <label htmlFor="client-active">
            <input
              checked={isActive}
              id="client-active"
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
