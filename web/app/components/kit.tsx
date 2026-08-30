import { X } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

export function PageHeader({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <div className="flex items-baseline gap-3">
        <h1 className="font-display text-[26px] font-bold tracking-tight text-ink">
          {title}
        </h1>
        {subtitle && (
          <span className="text-[13px] text-slate-500">{subtitle}</span>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-2">{children}</div>
    </div>
  );
}

export function StatStrip({
  items,
  cols = 4,
}: {
  items: { label: string; value: string; note?: ReactNode }[];
  cols?: 3 | 4;
}) {
  return (
    <div
      className={cn(
        "grid grid-cols-2 divide-slate-200 overflow-hidden rounded-md border border-slate-200 bg-white md:divide-x",
        cols === 3 ? "md:grid-cols-3" : "md:grid-cols-4",
      )}
    >
      {items.map((it) => (
        <div
          key={it.label}
          className="border-b border-slate-200 px-5 py-4 last:border-b-0 md:border-b-0"
        >
          <div className="text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">
            {it.label}
          </div>
          <div className="mt-1 font-display text-[26px] font-bold leading-none tabular-nums text-ink">
            {it.value}
          </div>
          {it.note && (
            <div className="mt-1.5 text-[11px] font-semibold tabular-nums">
              {it.note}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

export function Card({
  title,
  meta,
  children,
  className,
}: {
  title?: string;
  meta?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "overflow-hidden rounded-md border border-slate-200 bg-white",
        className,
      )}
    >
      {title && (
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-2.5">
          <h2 className="text-[11px] font-bold uppercase tracking-[0.12em] text-ink">
            {title}
          </h2>
          {meta && (
            <span className="text-[11px] tabular-nums text-slate-500">{meta}</span>
          )}
        </div>
      )}
      {children}
    </div>
  );
}

export function DetailPanel({
  eyebrow,
  title,
  onClose,
  children,
}: {
  eyebrow?: string;
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  return (
    <div className="overflow-hidden rounded-md border border-slate-200 bg-white">
      <div className="flex items-center justify-between bg-ink px-4 py-3">
        <div>
          {eyebrow && (
            <div className="text-[9px] font-bold uppercase tracking-[0.16em] text-white/45">
              {eyebrow}
            </div>
          )}
          <div className="font-display text-[15px] font-bold text-white">
            {title}
          </div>
        </div>
        <button
          onClick={onClose}
          type="button"
          className="rounded border border-white/25 px-2 py-1 text-[11px] font-medium text-white/80 transition-colors hover:bg-white/10"
        >
          Close
        </button>
      </div>
      <div className="p-4">{children}</div>
    </div>
  );
}

export function Stat({
  label,
  value,
  className,
}: {
  label: string;
  value: ReactNode;
  className?: string;
}) {
  return (
    <div className={className}>
      <div className="text-[9px] font-bold uppercase tracking-[0.14em] text-slate-500">
        {label}
      </div>
      <div className="mt-0.5 text-[15px] font-semibold tabular-nums text-ink">
        {value}
      </div>
    </div>
  );
}

export const btn = {
  primary:
    "inline-flex items-center gap-1.5 rounded bg-brand px-3.5 py-2 text-[13px] font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-50",
  ghost:
    "inline-flex items-center gap-1.5 rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50",
  danger:
    "inline-flex items-center gap-1.5 rounded border border-slate-300 bg-white px-3 py-1.5 text-[13px] font-medium text-red-600 transition-colors hover:bg-red-50 disabled:opacity-50",
};

export const field =
  "w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-[13px] text-ink outline-none transition-shadow focus:border-brand focus:ring-2 focus:ring-brand/20";

export const fieldInvalid =
  "border-red-500 focus:border-red-500 focus:ring-red-500/20";

export function Field({
  label,
  children,
  error,
}: {
  label: string;
  children: ReactNode;
  error?: string | undefined;
}) {
  return (
    <label className="block">
      <span className="mb-1 flex items-center justify-between gap-2 text-[10px] font-bold uppercase tracking-[0.12em] text-slate-500">
        {label}
      </span>
      {children}
      {error && (
        <span role="alert" className="mt-1 block text-[12px] font-medium text-red-600">
          {error}
        </span>
      )}
    </label>
  );
}

export function Modal({
  open,
  title,
  description,
  onClose,
  children,
}: {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
}) {
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-ink/50 p-6 backdrop-blur-[2px]">
      <div className="mt-10 w-full max-w-lg animate-in fade-in zoom-in-95 overflow-hidden rounded-md border border-slate-200 bg-white shadow-2xl duration-200">
        <div className="flex items-start justify-between border-b border-slate-200 bg-ink px-5 py-4">
          <div>
            <div className="font-display text-[15px] font-bold text-white">
              {title}
            </div>
            {description && (
              <div className="mt-0.5 text-[12px] text-white/50">{description}</div>
            )}
          </div>
          <button
            onClick={onClose}
            type="button"
            className="text-white/60 transition-colors hover:text-white"
            aria-label="Close"
          >
            <X className="size-4" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

export function Pagination({
  page,
  pages,
  onPage,
  label,
}: {
  page: number;
  pages: number;
  onPage: (p: number) => void;
  label: string;
}) {
  return (
    <div className="flex items-center justify-between border-t border-slate-200 px-4 py-2.5">
      <span className="text-[11px] tabular-nums text-slate-500">{label}</span>
      <div className="flex gap-2">
        <button
          type="button"
          className={btn.ghost}
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Prev
        </button>
        <button
          type="button"
          className={btn.ghost}
          disabled={page >= pages}
          onClick={() => onPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
}

export function AlertBanner({
  title,
  children,
  variant = "error",
}: {
  title: string;
  children: ReactNode;
  variant?: "error" | "info" | "empty";
}) {
  const styles =
    variant === "error"
      ? "border-red-500 bg-red-50"
      : variant === "info"
        ? "border-brand bg-brand/5"
        : "border-slate-300 bg-slate-50";

  return (
    <div className={cn("flex gap-3 rounded-md border-l-[3px] px-4 py-3", styles)}>
      <div>
        <div className="text-[13px] font-bold text-ink">{title}</div>
        <div className="mt-0.5 max-w-[80ch] text-[12px] leading-relaxed text-slate-600">
          {children}
        </div>
      </div>
    </div>
  );
}

export function LoadingBar(): React.JSX.Element {
  return (
    <div
      aria-label="Loading"
      aria-valuetext="Loading"
      className="h-0.5 w-full animate-pulse bg-brand"
      role="progressbar"
    />
  );
}

export const th =
  "px-4 py-2 text-[10px] font-bold uppercase tracking-[0.1em] text-slate-500";
export const td = "px-4 py-2.5 text-[13px] text-ink";
