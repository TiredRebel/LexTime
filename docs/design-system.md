# Design System — LexTime Dashboard

**Scope:** `web/app/globals.css`, `web/app/components/kit.tsx`, and the React
components that consume them.
**Status:** aligned with [timeflow-pro](https://github.com/TiredRebel/timeflow-pro)
"Precision Terminal Density" — Sora/Manrope, navy sidebar, brand-steel accent,
Tailwind v4 tokens. Update this file in the same commit as any visual change.

The dashboard is a thin static Next.js export served from `LexTime.Api/wwwroot`.
Node is required only when changing files under `web/`.

---

## Foundations

### Stack

| Piece | Role |
| --- | --- |
| Tailwind CSS v4 | Utility layout and component styling via `@theme` tokens |
| `globals.css` | oklch semantic colors, Sora/Manrope font registration |
| `components/kit.tsx` | Shared primitives: `PageHeader`, `StatStrip`, `Card`, `DetailPanel`, buttons, fields |
| `components/app-shell.tsx` | Persistent sidebar with Lucide icons and hash routing |
| `lucide-react` | Navigation and sign-in iconography |

### Color (oklch via `@theme`)

| Token | Role |
| --- | --- |
| `--color-ink` | Primary text, sidebar ground |
| `--color-brand` | Accent: active nav rail, primary buttons, positive deltas |
| `--color-canvas` | App background behind white cards |
| `--color-navy-900` / `--color-navy-800` | Depth variants |
| `--color-steel` | Secondary accent |

Components use Tailwind utilities (`bg-ink`, `text-brand`, `border-slate-200`)
— not inline hex.

### Typography

| Role | Family | Used for |
| --- | --- | --- |
| Display | Sora (`font-display`) | Page titles, KPI numerals, wordmark |
| Body | Manrope (`font-sans`) | Tables, forms, labels |

Loaded in `layout.tsx` from Google Fonts. Tabular numerals on all hour/amount
columns (`tabular-nums`).

### Radius and motion

- **Radius:** `0.625rem` base (`--radius`); cards and controls use `rounded-md`.
- **Motion:** 150ms hover tints on table rows; `@media (prefers-reduced-motion: reduce)` disables transitions globally.

---

## Components (`kit.tsx`)

| Export | Use when |
| --- | --- |
| `PageHeader` | View title + optional subtitle; action slot on the right |
| `StatStrip` | 3–4 KPI tiles in a divided white strip |
| `Card` | Bordered table container with optional title/meta header |
| `DetailPanel` | Navy-header side panel (forms, delete confirm) |
| `btn.primary` / `btn.ghost` / `btn.danger` | Commit, cancel/nav, destructive |
| `field` + `Field` | Text, date, select, textarea controls |
| `AlertBanner` | Non-happy states (error, empty, info) |
| `LoadingBar` | In-flight listing fetch |
| `th` / `td` | Table cell typography |

`detail-form.tsx` wraps `DetailPanel` for read-only entity chrome and
`DetailForm` for editable flows (client, matter, time entry).

---

## Layout

**App shell:** 212px sticky navy sidebar (desktop), off-canvas drawer below
`lg`. Lucide icons with a 3px brand rail on the active item. Sign-out in the
sidebar footer.

**List + detail:** Main column is a `Card` table; selection opens a
340px-detail column (`xl:grid-cols-[minmax(0,1fr)_340px]`). Clients nests
matters one level deep in the detail column.

**Sign-in:** Split layout from timeflow-pro — ink brand panel with feature
list, canvas panel with token field. Quickstart link preserved from LexTime.

---

## Building

```powershell
cd web
npm ci
npm run build
```

`npm run build` exports to `out/` and syncs into `src/LexTime.Api/wwwroot`.

---

## Source reference

Visual and component patterns are ported from
[timeflow-pro](https://github.com/TiredRebel/timeflow-pro). LexTime keeps its
existing API integration, hash routing, and domain behaviour — only the
presentation layer changed.
