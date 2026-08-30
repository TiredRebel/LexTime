import type { FormEvent, ReactNode } from "react";

import { btn, DetailPanel as KitDetailPanel } from "@/components/kit";

export interface DetailFormMessage {
  readonly detail: string;
  readonly id?: string;
  readonly rule: string;
}

interface DetailFormMessagesProps {
  readonly fieldError: string | null;
  readonly messages?: readonly DetailFormMessage[];
}

/**
 * Shared field-error / conflict / domain-rule-violation block used below every
 * detail form's fields.
 */
export function DetailFormMessages({
  fieldError,
  messages = [],
}: DetailFormMessagesProps): React.JSX.Element | null {
  if (fieldError === null && messages.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2" role="alert">
      {fieldError !== null && (
        <p className="text-[12px] font-medium text-red-600">{fieldError}</p>
      )}
      {messages.length > 0 && (
        <ul className="list-disc space-y-1 pl-4 text-[12px] text-red-600">
          {messages.map((message) => (
            <li key={message.id ?? `${message.rule}-${message.detail}`}>
              <span className="text-[10px] font-bold uppercase tracking-[0.08em] text-slate-600">
                {message.rule}
              </span>{" "}
              {message.detail}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

interface DetailPanelProps {
  readonly actions?: ReactNode;
  readonly children: ReactNode;
  readonly headerAction: ReactNode;
  readonly title: string;
  readonly titleId: string;
}

/** Read-only detail shell with navy header bar. */
export function DetailPanel({
  actions,
  children,
  headerAction,
  title,
  titleId,
}: DetailPanelProps): React.JSX.Element {
  return (
    <section aria-labelledby={titleId}>
      <div className="overflow-hidden rounded-md border border-slate-200 bg-white">
        <div className="flex items-center justify-between bg-ink px-4 py-3">
          <h2 className="font-display text-[15px] font-bold text-white" id={titleId}>
            {title}
          </h2>
          {headerAction}
        </div>
        <div className="space-y-4 p-4">{children}</div>
        {actions !== undefined && (
          <div className="flex flex-wrap justify-end gap-2 border-t border-slate-200 px-4 py-3">
            {actions}
          </div>
        )}
      </div>
    </section>
  );
}

interface DetailFormProps {
  readonly children: ReactNode;
  readonly fieldError: string | null;
  readonly isSubmitting: boolean;
  readonly messages?: readonly DetailFormMessage[];
  readonly onCancel: () => void;
  readonly onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  readonly submitLabel: string;
  readonly title: string;
  readonly titleId: string;
}

/** Editable detail form shell with Cancel/Save actions. */
export function DetailForm({
  children,
  fieldError,
  isSubmitting,
  messages,
  onCancel,
  onSubmit,
  submitLabel,
  title,
  titleId,
}: DetailFormProps): React.JSX.Element {
  return (
    <KitDetailPanel onClose={onCancel} title={title}>
      <form className="space-y-4" noValidate onSubmit={onSubmit}>
        {children}
        <DetailFormMessages fieldError={fieldError} messages={messages} />
        <div className="flex flex-wrap justify-end gap-2 pt-2">
          <button className={btn.ghost} onClick={onCancel} type="button">
            Cancel
          </button>
          <button className={btn.primary} disabled={isSubmitting} type="submit">
            {submitLabel}
          </button>
        </div>
      </form>
    </KitDetailPanel>
  );
}
