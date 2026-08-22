import type { FormEvent, ReactNode } from "react";

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
 * detail form's fields. Exported separately so a bespoke panel (time entry
 * delete) can reuse it without going through the full DetailForm wrapper.
 */
export function DetailFormMessages({
  fieldError,
  messages = [],
}: DetailFormMessagesProps): React.JSX.Element | null {
  if (fieldError === null && messages.length === 0) {
    return null;
  }

  return (
    <div className="form-messages" role="alert">
      {fieldError !== null && <p className="field-error">{fieldError}</p>}
      {messages.length > 0 && (
        <ul className="violation-list">
          {messages.map((message) => (
            <li key={message.id ?? `${message.rule}-${message.detail}`}>
              <span className="violation-rule">{message.rule}</span>
              {message.detail}
            </li>
          ))}
        </ul>
      )}
    </div>
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

/**
 * Shared chrome for the client / matter / time-entry detail forms: the
 * detail-pane section, its header (title + Close), the form element, the
 * field/conflict/violation messages, and the Cancel/Save actions. Each
 * entity's own field markup is passed as children.
 */
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
    <section aria-labelledby={titleId} className="detail-pane">
      <div className="detail-header">
        <h2 id={titleId}>{title}</h2>
        <button
          aria-label="Close"
          className="secondary-button"
          onClick={onCancel}
          type="button"
        >
          Close
        </button>
      </div>
      <form className="entry-form" noValidate onSubmit={onSubmit}>
        {children}
        <DetailFormMessages fieldError={fieldError} messages={messages} />
        <div className="form-actions">
          <button className="secondary-button" onClick={onCancel} type="button">
            Cancel
          </button>
          <button className="primary-button" disabled={isSubmitting} type="submit">
            {submitLabel}
          </button>
        </div>
      </form>
    </section>
  );
}
