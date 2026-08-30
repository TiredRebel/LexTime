"use client";

import {
  BarChart3,
  CalendarDays,
  Folder,
  LogOut,
  User,
} from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

export type ShellDestination =
  | "reports"
  | "time-entries"
  | "clients"
  | "timekeepers";

const NAV: readonly {
  destination: ShellDestination;
  hash: string;
  icon: typeof CalendarDays;
  label: string;
}[] = [
  { hash: "#time-entries", destination: "time-entries", label: "Time entries", icon: CalendarDays },
  { hash: "#clients", destination: "clients", label: "Clients", icon: Folder },
  { hash: "#timekeepers", destination: "timekeepers", label: "Timekeepers", icon: User },
  { hash: "#reports", destination: "reports", label: "Reports", icon: BarChart3 },
];

interface AppShellProps {
  readonly children: ReactNode;
  readonly destination: ShellDestination;
  readonly navOpen: boolean;
  readonly onNavClose: () => void;
  readonly onNavToggle: () => void;
  readonly onSignOut: () => void;
}

/** Persistent sidebar shell matching the timeflow-pro layout, adapted for hash routing. */
export function AppShell({
  children,
  destination,
  navOpen,
  onNavClose,
  onNavToggle,
  onSignOut,
}: AppShellProps): React.JSX.Element {
  return (
    <div className="flex min-h-screen bg-canvas font-sans text-ink antialiased">
      <header className="sticky top-0 z-30 flex items-center justify-between gap-4 bg-ink px-5 py-3.5 lg:hidden">
        <div className="font-display text-[17px] font-extrabold tracking-[0.06em] text-white">
          LEXTIME
        </div>
        <button
          aria-controls="app-sidebar"
          aria-expanded={navOpen}
          type="button"
          onClick={onNavToggle}
          className="rounded border border-white/20 px-3 py-2 text-[13px] font-semibold text-white"
        >
          {navOpen ? "Close" : "Menu"}
        </button>
      </header>

      {navOpen && (
        <button
          aria-label="Close menu"
          type="button"
          className="fixed inset-0 z-40 bg-ink/50 lg:hidden"
          onClick={onNavClose}
        />
      )}

      <nav
        className={cn(
          "fixed inset-y-0 left-0 z-50 flex h-full w-[212px] shrink-0 -translate-x-full flex-col bg-ink transition-transform duration-200 lg:sticky lg:top-0 lg:z-auto lg:translate-x-0 lg:transition-none",
          navOpen && "translate-x-0",
        )}
        id="app-sidebar"
      >
        <a
          href="#reports"
          className="block px-6 py-7 font-display text-[17px] font-extrabold tracking-[0.06em] text-white"
          onClick={onNavClose}
        >
          LEXTIME
        </a>

        <div className="flex flex-col gap-0.5 px-2">
          {NAV.map((item) => {
            const active = destination === item.destination;
            return (
              <a
                key={item.hash}
                href={item.hash}
                aria-current={active ? "page" : undefined}
                onClick={onNavClose}
                className={cn(
                  "group relative flex items-center gap-3 rounded px-4 py-2.5 text-[13px] font-medium transition-colors duration-150",
                  active
                    ? "bg-white/[0.07] text-white"
                    : "text-white/60 hover:bg-white/[0.04] hover:text-white",
                )}
              >
                <span
                  className={cn(
                    "absolute bottom-1.5 left-0 top-1.5 w-[3px] rounded-full bg-brand transition-opacity duration-150",
                    active ? "opacity-100" : "opacity-0",
                  )}
                />
                <item.icon
                  className={cn("size-4", active ? "text-brand" : "")}
                  strokeWidth={1.8}
                />
                {item.label}
              </a>
            );
          })}
        </div>

        <div className="mt-auto flex items-center justify-between border-t border-white/10 px-6 py-5">
          <span className="text-[11px] text-white/40">
            Signed in as{" "}
            <span className="font-semibold text-white/70">Reviewer</span>
          </span>
          <button
            type="button"
            onClick={onSignOut}
            className="text-white/40 transition-colors hover:text-white"
            title="Sign out"
          >
            <LogOut className="size-3.5" strokeWidth={1.8} />
          </button>
        </div>
      </nav>

      <main
        className="flex min-w-0 flex-1 flex-col gap-5 overflow-x-hidden p-5 lg:p-7"
        id="main-content"
      >
        {children}
      </main>
    </div>
  );
}
