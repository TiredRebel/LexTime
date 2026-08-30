import {
  ArrowRight,
  CalendarDays,
  Folder,
  Lock,
  Plus,
  Shield,
} from "lucide-react";
import type { FormEvent } from "react";

import { AlertBanner, btn, field, LoadingBar } from "@/components/kit";
import { cn } from "@/lib/utils";

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

const FEATURES = [
  { icon: Plus, text: "Per-client weekly rollup, ranked and cumulative" },
  { icon: CalendarDays, text: "Six-minute-increment time entries, by matter" },
  { icon: Folder, text: "Clients, matters and timekeepers in one directory" },
] as const;

export function DevelopmentTokenPrompt({
  message,
  onSubmit,
  onTokenChange,
  token,
}: DevelopmentTokenPromptProps): React.JSX.Element {
  return (
    <main
      className="grid min-h-screen font-sans text-ink lg:grid-cols-[minmax(0,42%)_minmax(0,58%)]"
      id="main-content"
    >
      <aside className="relative overflow-hidden bg-ink px-12 py-16 text-white">
        <div className="pointer-events-none absolute -right-24 -top-24 size-[420px] rounded-full border border-white/[0.06]" />
        <div className="pointer-events-none absolute -right-4 top-8 size-[220px] rounded-full border border-white/[0.06]" />
        <div className="relative flex h-full max-w-[420px] flex-col">
          <div>
            <div className="font-display text-[17px] font-extrabold tracking-[0.06em]">
              LEXTIME
            </div>
            <div className="mt-3 h-[3px] w-8 rounded-full bg-brand" />
          </div>

          <h1 className="mt-14 font-display text-[46px] font-extrabold leading-[1.05] tracking-tight">
            Every billable hour, accounted for.
          </h1>
          <p className="mt-6 max-w-[46ch] text-[14px] leading-relaxed text-white/55">
            Weekly rollups, time entries and client standing — read straight
            from the same report the firm already computes.
          </p>

          <ul className="mt-12 space-y-4">
            {FEATURES.map((feature) => (
              <li key={feature.text} className="flex items-center gap-4">
                <span className="flex size-8 shrink-0 items-center justify-center rounded bg-brand/15 text-brand">
                  <feature.icon className="size-4" strokeWidth={1.8} />
                </span>
                <span className="text-[13px] text-white/70">{feature.text}</span>
              </li>
            ))}
          </ul>

          <div className="mt-auto flex items-center gap-4 border-t border-white/10 pt-6 text-[12px] text-white/35">
            <span>support@lextime.dev</span>
            <span>·</span>
            <span>Status: operational</span>
          </div>
        </div>
      </aside>

      <section className="flex items-center justify-center bg-canvas px-8 py-16">
        <form className="w-full max-w-[400px]" onSubmit={onSubmit}>
          <div className="font-display text-[13px] font-extrabold tracking-[0.12em] text-slate-500">
            LEXTIME
          </div>
          <div className="mt-2 text-[11px] font-bold uppercase tracking-[0.14em] text-brand">
            Local reviewer access
          </div>
          <h2 className="mt-2 font-display text-[26px] font-bold tracking-tight text-ink">
            Open the weekly rollup
          </h2>
          <p className="mt-2 text-[13px] leading-relaxed text-slate-500">
            Paste the development token printed by{" "}
            <code className="rounded bg-slate-100 px-1 py-0.5 text-[12px]">
              Initialize-LocalDb.ps1
            </code>
            . It stays in this tab only.
          </p>
          <p className="mt-2 text-[13px] leading-relaxed text-slate-500">
            Don&rsquo;t have one?{" "}
            <a
              className="font-medium text-brand underline-offset-2 hover:underline"
              href="https://github.com/TiredRebel/LexTime#quickstart"
              rel="noreferrer"
              target="_blank"
            >
              Run the two-command quickstart
            </a>{" "}
            — it prints a fresh token to paste here.
          </p>

          <div className="mt-7 space-y-4">
            <label className="block">
              <span className="mb-1.5 block text-[12px] font-semibold text-ink">
                Development token
              </span>
              <div className="relative">
                <Lock className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-slate-400" />
                <input
                  aria-describedby={message === null ? undefined : "token-error"}
                  aria-invalid={message !== null}
                  autoComplete="off"
                  className={cn(field, "py-2.5 pl-9", message !== null && "border-red-500")}
                  id="development-token"
                  onChange={(event) => onTokenChange(event.target.value)}
                  placeholder="eyJhbGciOi…"
                  spellCheck={false}
                  type="password"
                  value={token}
                />
              </div>
            </label>
            {message !== null && (
              <p className="text-[12px] font-medium text-red-600" id="token-error" role="alert">
                {message}
              </p>
            )}
            <button className={cn(btn.primary, "w-full justify-center py-3")} type="submit">
              Open dashboard <ArrowRight className="size-4" />
            </button>
          </div>

          <div className="mt-6 flex items-center gap-2 border-t border-slate-200 pt-5 text-[12px] text-slate-500">
            <Shield className="size-3.5 text-slate-400" />
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
      return <LoadingBar />;
    case "blocked-range":
      return (
        <AlertBanner title="No report shown" variant="error">
          Fix the date range above — its start can&rsquo;t be after its end — to
          see the weekly rollup again.
        </AlertBanner>
      );
    case "empty":
      return (
        <AlertBanner title="No report activity in this period" variant="empty">
          The report completed successfully, but no rows exist between the
          selected dates. Try a broader period.
        </AlertBanner>
      );
    case "unauthenticated":
      return (
        <AlertBanner title="Development session expired" variant="error">
          {state.message}
        </AlertBanner>
      );
    case "unavailable":
      return (
        <div className="space-y-3">
          <AlertBanner title="Report service unavailable" variant="error">
            {state.message}
          </AlertBanner>
          <button className={btn.ghost} onClick={onRetry} type="button">
            Try again
          </button>
        </div>
      );
    default: {
      const unhandledState: never = state;
      return unhandledState;
    }
  }
}

export function ClientFilterEmptyStatus(): React.JSX.Element {
  return (
    <AlertBanner title="No activity matches this client" variant="info">
      The period contains report rows, but none match the current client filter.
      Choose All clients to restore them.
    </AlertBanner>
  );
}
