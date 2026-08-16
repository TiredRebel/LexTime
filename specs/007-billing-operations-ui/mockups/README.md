# Mockups — Billing Dashboard (007)

Copied from `C:\Users\mcgun\.codex\generated_images\01a00621-8478-7d61-803b-3f7d6e8edce0`
on 2026-08-16. That folder is the design source; these files are the in-repo copy so a
reviewer does not need Codex paths.

The eighth file in `.codex/generated_images` (`019fd703-…`, an automation banner) is
not LexTime and is not copied.

| File | Screen | This spec |
| --- | --- | --- |
| [01-sign-in.png](./01-sign-in.png) | Split navy / sign-in card | **Chrome only.** Token paste, not email/password (R3, FR-014) |
| [02-overview.png](./02-overview.png) | Weekly billing overview, charts, recent entries | Visual language. Charts and "Record time" are not this slice |
| [03-weekly-billable-rollup.png](./03-weekly-billable-rollup.png) | Reports → Weekly billable rollup | **Primary screen for 007** |
| [04-time-entries.png](./04-time-entries.png) | Time entries list and detail | Feature 008 |
| [05-clients.png](./05-clients.png) | Clients list and detail | Feature 009 |
| [06-matters.png](./06-matters.png) | Matters list and detail | Feature 009 |
| [07-timekeepers.png](./07-timekeepers.png) | Timekeepers, marked read-only | Feature 009 |
| [08-settings.png](./08-settings.png) | Profile, theme, notifications, session | Out of scope (prefs, IdP-shaped settings, RBAC-looking admin) |

## Visual language this slice does take

From every screen, consistently:

- Navy sidebar, serif "LexTime" wordmark, sans-serif UI type
- White main canvas, light cards, blue for selected nav and links
- Green / red only as secondary cues next to text (FR-011)

From [03-weekly-billable-rollup.png](./03-weekly-billable-rollup.png), as data
the existing rollup can actually supply:

- Title "Weekly billable rollup"
- Inclusive date range and client filter in the header
- Table "Weekly rollup by client": client, week, billable hours, non-billable,
  amount, cumulative, delta, rank
- Empty / loading / error as distinct states, not a blank table

## What the mockups show that 007 must not ship

These need data or behaviour the current API and this spec do not have:

| Mockup element | Why it stays out |
| --- | --- |
| Email / password / forgot password | No new identity provider (FR-014, R3) |
| Daily bar + last-week line chart | Rollup is per client per ISO week, not per day |
| "↑ 12.4% vs prior period" KPI deltas | Not a field on the rollup. Inventing them fails FR-013 |
| Export report | Not in the spec |
| Record time, search, recent entries | Feature 008 |
| Active matters / timekeeper / entry counts in the summary rail | Not in the rollup response |
| Working nav to Time entries, Clients, Matters, Timekeepers, Settings | FR-001 / SC-007 |
| Settings (theme, notifications, workspace prefs) | Product chrome; §2.2 still excludes a product frontend |

Sidebar in this slice: wordmark + **Reports** as the current page. Other
destinations from the mockup IA are not offered as actions. They return in 008
and 009, using the same chrome.
