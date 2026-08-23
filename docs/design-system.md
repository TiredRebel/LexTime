# Design System — LexTime Dashboard

**Scope:** `web/app/globals.css` and the React components that consume it.
**Status:** current as of `46ad7f1` — every token, class and component below
exists in the repo today; nothing here is aspirational. Update this file in
the same commit as any change to `globals.css` or the shared components
(`detail-form.tsx`, `dashboard-status.tsx`) — a stale design doc is worse
than none, per the project's own "document everything" standard.

The dashboard has one deliberate visual identity: a legal-pad reading —
sage `canvas`, off-white `paper` cards, a deep navy sidebar, serif headings,
a single red "margin" rule borrowed from a ruled legal pad rather than a
generic danger red. Flat by design: `border-radius` is `0` everywhere and
`--shadow` is `none`, applied explicitly rather than left as an accident of
omission.

---

## Foundations

### Color

| Token | Value | Used for |
| --- | --- | --- |
| `--canvas` | `#e7ece8` | Page background |
| `--paper` | `#f6f7f4` | Cards, panels, inputs, sign-in card |
| `--line` | `#d4d8d3` | Hairline borders, table row dividers |
| `--control-border` | `#b7bfb8` | Input/button borders (one step darker than `--line`) |
| `--ink-900` | `#1a1714` | Primary text — 16.6:1 on paper |
| `--ink-700` | `#3f3a36` | Field labels, form messages — 10.4:1 |
| `--ink-500` | `#6b6460` | Secondary/meta text, table headers — 5.4:1 |
| `--navy-950` | `#081825` | Sidebar and sign-in brand panel ground |
| `--navy-900` | `#102c45` | Primary button fill, client-name text, focus ring |
| `--navy-800` | `#1a3f5c` | Primary button hover, loading bar, info-panel rule |
| `--margin` | `#9e2b2f` | The one accent: current-nav rail, card rule, negative delta, errors — 6.9:1 |
| `--red` | `var(--margin)` | Alias, not a separate color — see Known history below |
| `--red-soft` | `#f7ecec` | `.status-error` background |
| `--teal` | `#126056` | "Active" status, positive delta — 6.2–6.9:1 on paper/canvas |
| `--blue-700` | `#1f4e79` | Skip-link border only |

Every color is a custom property; there is no inline hex in a component
selector. `--blue-700` is intentionally the only single-use color token —
it exists because the accessibility skip-link needs a border distinct from
everything else on the page.

**Known history:** `--teal` was `#1a8b7d` (3.5–3.9:1, failing WCAG AA for
normal text) until it was measured and corrected. `--red` used to be a
second literal `#9e2b2f` — the identical hex as `--margin`, declared
independently — until it was aliased. If you're about to add a new color,
check it isn't a near-duplicate of one already here first.

### Typography

Two families:

| Role | Stack | Used for |
| --- | --- | --- |
| Display / serif | `"Palatino Linotype", Palatino, "Book Antiqua", Georgia, serif` | Wordmark, page/panel titles, KPI values, rank badge, sign-in headline |
| Body / sans | `"Segoe UI", system-ui, sans-serif` | Everything else |

Size scale (`--text-*`), 15 tokens covering every `font-size` in the file:

| Token | Value | Token | Value |
| --- | --- | --- | --- |
| `--text-3xs` | `0.68rem` | `--text-2xl` | `1.45rem` |
| `--text-2xs` | `0.7rem` | `--text-3xl` | `1.7rem` |
| `--text-xs` | `0.82rem` | `--text-4xl` | `1.85rem` |
| `--text-sm` | `0.84rem` | `--text-5xl` | `2.1rem` |
| `--text-base` | `1rem` | `--text-6xl` | `2.4rem` |
| `--text-md` | `1.05rem` | `--text-title` | `clamp(1.55rem, 2.2vw, 2.2rem)` |
| `--text-lg` | `1.25rem` | `--text-hero` | `clamp(var(--text-6xl), 4vw, 4.5rem)` |
| `--text-xl` | `1.35rem` | | |

Money and hours carry `font-variant-numeric: tabular-nums` wherever they
appear in a column (`.data-table td`, `.summary-value`, `.rank-badge`) —
keep that on any new numeric display.

### Spacing

33 tokens (`--space-<rem·20>`), a straight 0.05rem (0.8px) grid — `--space-3`
is `0.15rem`, `--space-20` is `1rem`, `--space-110` is `5.5rem`. Every
`padding`/`margin`/`gap` value in the file uses one of these, with one
deliberate exception: the three responsive `clamp()` paddings
(`.sign-in-brand`, `.sign-in-card`, `.main-content`) stay as literal
`clamp()` expressions — they're one-off responsive values, not a repeated
rhythm, so wrapping them in a token would only rename them.

**Coupled values stay coupled.** `.detail-pane`'s padding
(`--space-23 --space-25 --space-27`) is exactly cancelled by
`.detail-header`'s negative margin (the header bleeds to the pane's edges).
Both sides reference the *same* tokens — if you ever need to change that
padding, change it once and the header still lines up. Don't hand-tune one
side without the other.

Pick the nearest existing step before adding a new one. If nothing within
`--space-3`…`--space-110` fits, that's a real new step — add it in numeric
order with the rest.

### Radius, shadow, motion

- **Radius:** `0` on every button/input/select/textarea — one global reset,
  not a token, because there's no variation to name.
- **Shadow:** `--shadow: none`, applied explicitly via `box-shadow: var(--shadow)`
  on every bordered panel/card/button (`.primary-button`/`.secondary-button`/
  `.danger-button`, `.summary-card`, `.list-panel`, `.detail-pane`,
  `.status-panel`). Depth is drawn with a 1px `--line` border plus, on
  emphasized panels, a 3px left rule in `--margin` — never a box-shadow.
- **Motion:** three durations, all inline (no token — three uses isn't
  enough to justify one): `0.2s ease` (sidebar slide), `0.12s ease` (row
  hover), `1.4s ease-in-out infinite` (loading bar pulse).
  `@media (prefers-reduced-motion: reduce)` zeroes all transitions globally
  and swaps the loading bar's pulse for a static bar — keep that guard on
  anything new that animates.

---

## Components

### Buttons

`.primary-button` / `.secondary-button` / `.danger-button` — one size
(`--control-height`, 2.6rem, 2.75rem under `pointer: coarse`), three fills.

| Variant | Fill | Hover | Use when |
| --- | --- | --- | --- |
| `.primary-button` | `--navy-900` | `--navy-800` | The one committing action per view — Save, Apply, Record time |
| `.secondary-button` | `--paper` | `--canvas` | Cancel, Close, Previous/Next, Try again |
| `.danger-button` | `--red` | `filter: brightness(0.95)` | The single destructive confirm — delete a time entry |

**States:** default, hover, `:disabled` (`opacity: 0.45`, `cursor: not-allowed`).
No loading/spinner state — an in-flight submit disables the button
(`disabled={isSubmitting}`) with no other visual feedback.

**Accessibility:** focus is the global 2px navy `:focus-visible` outline;
no per-component override.

```tsx
<button className="primary-button" disabled={isSubmitting} type="submit">
  Save entry
</button>
```

### Form fields

`.field` wraps every input type with one shape: label, control, optional
error.

| Variant | Class | Notes |
| --- | --- | --- |
| Text / date / number | `.field input` | 2.6rem tall, 1px `--control-border` |
| Select | `.field select` | Hand-drawn SVG chevron via `--select-chevron`, `appearance: none` |
| Checkbox | `.checkbox-field` | Label wraps input, `accent-color: var(--navy-900)` |
| Read-only value | `.readonly-value` | Same height as a live input — used for immutable fields (matter number, client code) in edit mode |
| Textarea | `.field textarea` | Vertical resize only, 6.5rem minimum |

**States:** default, `:focus-visible`, and `[aria-invalid="true"]` → red
border (`.field input[aria-invalid="true"]` etc.) — paired with the
existing `.field-error` text below the field. Set `aria-invalid` and
`aria-describedby` together on any new validated field; the border comes
for free once you do.

```tsx
<div className="field">
  <label htmlFor="entry-date">Work date</label>
  <input
    id="entry-date"
    type="date"
    aria-invalid={hasError}
    aria-describedby={hasError ? "date-error" : undefined}
  />
</div>
```

### Status panel

One component, three visual modifiers, driving every non-happy state in
every list view.

| Modifier | Rule color | `role` | Use when |
| --- | --- | --- | --- |
| `.status-info` | `--navy-800` | `status` | Filter narrowed results to nothing (data exists elsewhere) |
| `.status-empty` | `--line` (neutral) | `status` | Query genuinely has zero rows — not an error |
| `.status-error` | `--margin` | `alert` | Session expired, API unreachable, or the date range is invalid — carries a "Try again" action when retryable |

```tsx
<section className="status-panel status-error" role="alert">
  <div>
    <h2>No report shown</h2>
    <p>Fix the date range above — its start can't be after its end — to see the weekly rollup again.</p>
  </div>
</section>
```

**Every list view runs an explicit state machine** — `idle` / `loading` /
`ready` / `empty` / `blocked-range` / `unauthenticated` / `unavailable` /
(single-entity) `missing` — and the switch that renders this component ends
in `const unhandled: never = state`, so adding a state anywhere without
teaching every view how to render it fails `tsc`, not runtime. Keep that
pattern; it's the reason `blocked-range` used to silently unmount the whole
page instead of showing a panel — the `never` check doesn't catch a case
that returns `null` on purpose, only a case nobody wrote at all.

### Data table

One `.data-table` class, one `.data-table--wide` modifier (adds
`min-width: 52rem` at ≥721px) — it is the *same* table component for
Reports, Clients, Matters, Timekeepers and Time entries, not two unrelated
ones. Numerals are right-aligned and tabular; the first two columns
(identity) are left-aligned.

**Responsive:** below 720px the header row is visually hidden (clipped, not
`display: none`, so it stays in the accessibility tree) and each `<tr>`
becomes a bordered card with a `--margin`-red left rule; every cell prints
its column name via `content: attr(data-label)`.

```tsx
<table className="data-table data-table--wide">
  <thead><tr><th scope="col">Client</th>…</tr></thead>
  <tbody>
    <tr><td data-label="Client">…</td></tr>
  </tbody>
</table>
```

### Rank badge & delta

```css
.rank-badge   { /* serif numeral chip, --navy-900 on transparent */ }
.delta-positive { color: var(--teal); }
.delta-negative { color: var(--red); }
.delta-none     { color: var(--ink-500); }
```

The rank badge is the only place in the app that puts a numeral in the
serif face at small size — it reads as a printed index number rather than
a data-grid cell. `.status-active` (Active/Inactive flags) shares the same
`--teal` token as `.delta-positive` — that's one token driving both, so a
future contrast fix only ever needs to happen in one place.

### Detail pane / Detail panel — `detail-form.tsx`

Every entity — client, matter, timekeeper, time entry — opens the same
`.detail-pane` shell on selection: header (title + action), body, optional
footer actions. Two exported components share it:

```ts
export function DetailPanel(props: {
  actions?: ReactNode;      // optional footer row, e.g. Edit/Delete
  children: ReactNode;      // the <dl class="detail-list"> body
  headerAction: ReactNode;  // usually a Close button
  title: string;
  titleId: string;
}): React.JSX.Element;
```

Use `DetailPanel` for **read-only** views (`ClientDetail`, the inline
matter detail, `TimeEntryDetail`, the inline timekeeper detail).

```ts
export function DetailForm(props: {
  children: ReactNode;                        // the form's own <div className="field"> markup
  fieldError: string | null;
  isSubmitting: boolean;
  messages?: readonly DetailFormMessage[];     // conflict / domain-rule violations
  onCancel: () => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  submitLabel: string;
  title: string;
  titleId: string;
}): React.JSX.Element;
```

Use `DetailForm` for **editable** views (`ClientForm`, `MatterForm`,
`TimeEntryForm`'s record/revise modes). It wraps `DetailFormMessages` —
export that separately if a bespoke panel needs the field-error/violation
block without the rest of the form chrome (time entry's delete
confirmation does exactly this).

**Responsive:** below 1100px the pane becomes a `position: sticky` bottom
sheet (`max-height: min(72vh, 40rem)`) over the table, driven by
`.entries-workspace:has(.detail-pane)` — a pure-CSS `:has()` switch, no JS
layout branch. Worth knowing if this ever needs to run somewhere `:has()`
isn't supported (Firefox < 121, per caniuse).

**Do / Don't**

| ✅ Do | ❌ Don't |
| --- | --- |
| Add a 4th entity's read-only view through `DetailPanel` | Hand-roll another `<section className="detail-pane">` header block |
| Reuse `DetailFormMessages` for a new bespoke confirm panel | Duplicate the field-error/violation-list rendering again |

### Navigation

One nav, two renderings: a persistent 13.5rem rail on desktop, a full-height
slide-in drawer behind a "Menu" topbar button at ≤960px (dimmed backdrop,
Escape-to-close). Icons are single serif letters (T/C/K/R) — deliberate,
matches the wordmark's serif — not a placeholder for a real icon set.

### Sign-in gate

Split navy/paper screen. There is no email/password anywhere in the shipped
app — a single password-masked field takes a development bearer token
printed by `Initialize-LocalDb.ps1`. States: default and the empty-submit
`.field-error`. No in-flight ("verifying…") state exists today — the button
just submits; a real backend call here would need one.

---

## Patterns

**List + detail.** A `.list-panel` table drives a `.detail-pane`; selecting
a row is the only way in. Clients nests a second instance inside the pane
(a client's Matters), so the pattern recurses one level deep. Only the
code/name cell is the actual click target — the whole row tints on
selection, which overstates how much of it is interactive; that's a known,
not-yet-fixed affordance gap.

**Report controls.** A sticky filter bar (`position: sticky; top: 0`) above
every list — date range, an entity filter, one primary "Apply". Filters
never auto-apply; every change waits for the explicit submit.

**Record → revise → delete.** Time entries is the one entity with a full
write lifecycle, and `TimeEntryForm` takes a `mode` prop
(`"record" | "revise" | "delete"`) rather than being three components.
Delete renders as a confirmation sentence inside the same `.detail-pane`
chrome, not a modal.

---

## Responsive rules

| Breakpoint | What changes |
| --- | --- |
| `max-width: 1100px` | List+detail collapses to one column; detail pane becomes a sticky bottom sheet |
| `max-width: 960px` | Sidebar becomes an off-canvas drawer; topbar with Menu button appears |
| `max-width: 720px` | Tables become stacked cards; pagination stacks; sign-in becomes single column |
| `max-width: 480px` | Card rows drop to a single column each |
| `pointer: coarse` | `--control-height` grows 2.6rem → 2.75rem |
| `prefers-reduced-motion: reduce` | All transitions off; loading bar stops pulsing |

---

## Open items

Not fixed, not silently ignored either:

- **Row affordance.** Only the code/name cell in a directory row is
  clickable; nothing at rest signals that (no chevron, no cursor cue) —
  see "List + detail" above.
- **Read-only detail views vs. forms.** `DetailPanel` now covers the four
  read-only sites, but there's no similar consolidation for anything else
  that might grow a fifth or sixth hand-rolled `.detail-pane` user later —
  keep using it.
