import { type FormEvent, useState } from "react";

import { Field, field } from "@/components/kit";

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
        <Field label="Client code">
          <input
            autoComplete="off"
            className={field}
            id="client-code"
            onChange={(event) => setClientCode(event.target.value)}
            required
            value={clientCode}
          />
        </Field>
      ) : (
        <Field label="Client code">
          <p className="flex min-h-[2.25rem] items-center text-[13px] text-ink">
            {client?.clientCode}
          </p>
        </Field>
      )}
      <Field label="Client name">
        <input
          className={field}
          id="client-name"
          onChange={(event) => setName(event.target.value)}
          required
          value={name}
        />
      </Field>
      {mode === "correct" && (
        <label className="flex items-center gap-2 text-[13px] font-medium text-ink">
          <input
            checked={isActive}
            className="size-4 accent-brand"
            id="client-active"
            onChange={(event) => setIsActive(event.target.checked)}
            type="checkbox"
          />
          {formatActiveFlag(isActive)}
        </label>
      )}
    </DetailForm>
  );
}
