---
name: frontend-quality
description: The dashboard-specific UI quality bar for this take-home — what "polished B2B/SaaS" means here and the async-state contract every block must honor. Load when building or reviewing the React dashboard. Scoped to this assignment; it is not a general frontend style guide.
---

# Frontend quality bar — this dashboard only

Target viewport **1440×900**, desktop only. Formatting rules and null/zero semantics come from
[`sales-domain`](../sales-domain/SKILL.md); this skill covers layout, states, and interaction.

## The polished minimum (get this excellent before anything extra)

Header + period controls · six KPI cards · one clear trend chart · manager ranking with a
Gross-Profit / Average-Check switch · categories · top products · recent-sales table.

## Async-state contract — every async block honors all five

1. **Initial loading** → skeletons shaped like the real content (not a bare spinner, never a blank box).
2. **Refetch on period change** → keep the **previous snapshot visible**, dimmed, with a clear
   **"Updating…"** indicator. Never blank the screen or flip back to skeletons on a period change.
3. **API error** → an explicit error card with a **Retry** action. Never an infinite spinner.
4. **Whole-period empty** → one clear "No paid sales in this period" state for the page.
5. **Block-level empty** → each block (ranking, trend, categories, products, recent sales) has its own
   empty-state; one empty block never blanks the others.

There is one `GET /api/dashboard` call per period; TanStack Query keys it by the resolved period and
its `placeholderData`/`keepPreviousData` drives state (2). The ranking switch just **swaps which
server-ranked collection is displayed** (`rankings.grossProfit` / `rankings.averageCheck`) — no
refetch, and the client never re-sorts or re-ranks.

## Visual & interaction bar
- Clear hierarchy: KPI cards read first; one accent color; consistent spacing scale and typography.
- Information-dense but uncluttered — generous whitespace, aligned numerals (tabular figures for money).
- **Motion:** CSS/Tailwind transitions only (hover, dimming, bar/line easing). **No Framer Motion.**
  Subtle, never a presentation.
- **Accessibility basics:** keyboard-reachable period controls and ranking switch, visible focus
  rings, sufficient contrast, and **status shown by more than color** (badge text/icon for
  Paid/Cancelled/Refunded), so a refunded row is unmistakable without relying on hue.
- **Never render `NaN`, `Infinity`, `undefined`, or a raw null** — a null Margin/Average Check/delta
  shows as "—" or "New" per `sales-domain`.

## Verify in the running app (not from source)
Use the browser tooling at 1440×900: switch every preset and a custom range, toggle the ranking mode,
force an error (stop the api) to see the error+Retry state, pick a far-past custom range to see the
empty state, and check the console for errors and the network tab for failed requests. Capture a
screenshot as evidence.
