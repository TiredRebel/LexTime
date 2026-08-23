import type { FormEvent } from "react";

import type { DashboardState } from "./dashboard-state";

interface ReportStatusProps {
  readonly onRetry: () => void;
  readonly state: DashboardState;
}

interface DevelopmentTokenPromptProps {
  readonly message: string | null;
  readonly onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  readonly onTokenChange: (token: string) => void;
  readonly token: string;
}

export function DevelopmentTokenPrompt({
  message,
  onSubmit,
  onTokenChange,
  token,
}: DevelopmentTokenPromptProps): React.JSX.Element {
  return (
    <main className="sign-in-page" id="main-content">
      <section className="sign-in-brand" aria-label="LexTime">
        <div className="wordmark">LexTime</div>
        <span className="brand-rule" />
        <h1>The billing week, made legible.</h1>
        <p>
          Read the same cumulative hours, prior-week change and client standing
          the SQL report computed—without rewriting any of it.
        </p>
      </section>
      <section className="sign-in-content">
        <form className="sign-in-card" onSubmit={onSubmit}>
          <span className="wordmark">LexTime</span>
          <p className="eyebrow">Local reviewer access</p>
          <h2>Open the weekly rollup</h2>
          <p className="muted">
            Paste the development token printed by{" "}
            <code>Initialize-LocalDb.ps1</code>. It stays in this tab only.
          </p>
          <p className="muted">
            Don&rsquo;t have one?{" "}
            <a
              href="https://github.com/TiredRebel/LexTime#quickstart"
              rel="noreferrer"
              target="_blank"
            >
              Run the two-command quickstart
            </a>{" "}
            &mdash; it prints a fresh token to paste here.
          </p>
          <div className="field token-field">
            <label htmlFor="development-token">Development token</label>
            <input
              aria-describedby={message === null ? undefined : "token-error"}
              aria-invalid={message !== null}
              autoComplete="off"
              id="development-token"
              onChange={(event) => onTokenChange(event.target.value)}
              placeholder="eyJhbGciOi…"
              spellCheck={false}
              type="password"
              value={token}
            />
          </div>
          {message !== null && (
            <p className="field-error" id="token-error" role="alert">
              {message}
            </p>
          )}
          <button className="primary-button" type="submit">
            Open dashboard
          </button>
          <div className="security-note">
            <span aria-hidden="true" className="security-mark">
              ◇
            </span>
            No account, password or new identity provider.
          </div>
        </form>
      </section>
    </main>
  );
}

export function ReportStatus({
  onRetry,
  state,
}: ReportStatusProps): React.JSX.Element | null {
  switch (state.kind) {
    case "idle":
    case "ready":
      return null;
    case "loading":
      return (
        <div
          aria-label="Loading weekly rollup"
          aria-valuetext="Loading"
          className="loading-bar"
          role="progressbar"
        />
      );
    case "blocked-range":
      return (
        <section className="status-panel status-error" role="alert">
          <div>
            <h2>No report shown</h2>
            <p>
              Fix the date range above — its start can&rsquo;t be after its
              end — to see the weekly rollup again.
            </p>
          </div>
        </section>
      );
    case "empty":
      return (
        <section className="status-panel status-empty" role="status">
          <div>
            <h2>No report activity in this period</h2>
            <p>
              The report completed successfully, but no rows exist between the
              selected dates. Try a broader period.
            </p>
          </div>
        </section>
      );
    case "unauthenticated":
      return (
        <section className="status-panel status-error" role="alert">
          <div>
            <h2>Development session expired</h2>
            <p>{state.message}</p>
          </div>
        </section>
      );
    case "unavailable":
      return (
        <section className="status-panel status-error" role="alert">
          <div>
            <h2>Report service unavailable</h2>
            <p>{state.message}</p>
          </div>
          <div className="status-actions">
            <button
              className="secondary-button"
              onClick={onRetry}
              type="button"
            >
              Try again
            </button>
          </div>
        </section>
      );
    default: {
      const unhandledState: never = state;
      return unhandledState;
    }
  }
}

export function ClientFilterEmptyStatus(): React.JSX.Element {
  return (
    <section className="status-panel status-info" role="status">
      <div>
        <h2>No activity matches this client</h2>
        <p>
          The period contains report rows, but none match the current client
          filter. Choose All clients to restore them.
        </p>
      </div>
    </section>
  );
}
