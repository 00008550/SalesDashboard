---
name: sales-domain
description: THE single authoritative source of business rules, financial definitions, period/timezone semantics, and presentation formatting for the Sales Performance Dashboard. Load before implementing or reviewing any KPI, ranking, trend, category/product, recent-sales, period, or seed logic. If code and this file disagree, the code is wrong. Frozen 2026-09-02 by the final architecture correction — do not reinterpret.
---

# Sales domain — authoritative & frozen rules

The backend is the **single source of truth** for every calculation. The frontend only formats
values the API already computed; it never recomputes a financial figure.

## 1. Sale status → financial contribution

| Status | Revenue / Cost / Gross Profit | Paid-sale count | Operationally visible (Recent Sales) |
|---|---|---|---|
| **Paid** | Contributes | Counted | Yes |
| **Cancelled** | **Zero** | Excluded | Yes, with a clear badge |
| **Refunded** | **Zero net** | Excluded | Yes, with a clear badge |

**Refunded — simplified current-state interpretation (document in README):** without refund
timestamps or ledger entries, a refund **removes the sale from the financial aggregates of its
original sale period** — it does *not* create a later-period reversal. So a sale refunded later
still lowers the *original* period's revenue/profit. This is a deliberate, documented simplification;
the production evolution is a refund ledger with its own event date.

## 2. Metric definitions (identical in summary, ranking, trend, previous-period)

Compute **per Sale first**, then aggregate (see §6, the one-sale grain invariant).

- **Revenue** — additive, default **0** = Σ over Paid sales of Σ(item.salePrice × item.quantity).
- **Cost** — additive, default **0** = Σ over Paid sales of Σ(item.cost × item.quantity).
- **Gross Profit** = Revenue − Cost.
- **Paid Sales** — integer count, default **0** = number of Paid sales. (Always call it **"Paid
  Sales"** in API and UI — never bare "Sales" — because it is the Average Check denominator.)
- **Margin** = **null when Revenue == 0**, otherwise Gross Profit / Revenue.
- **Average Check** = **null when Paid Sales == 0**, otherwise Revenue / Paid Sales.
- **Best Manager** = the manager with the highest Gross Profit in the window; **null when no manager
  has Paid sales**. Ties broken deterministically (§4).

Identity that must always hold: **Revenue = Average Check × Paid Sales**.

Never emit NaN or Infinity from the API. A null ratio serializes as JSON `null`.

## 3. Deltas vs. previous period

- **Absolute delta** = current − previous (well-defined for additive metrics and counts).
- **Percentage delta** = (current − previous) / previous; **null when the previous baseline is 0**.
  The UI renders null as a neutral **"New"** or **"—"**, never Infinity/NaN.
- **Ratio metrics (Margin) are compared in percentage points (pp)**, not percent-of-percent:
  `marginDeltaPp = (currentMargin − previousMargin) × 100`. Null if either side is null.

## 4. Manager ranking — server-owned, two pre-ranked collections

- Ranking order and tie-breaking are computed **on the server**. The frontend never re-sorts or
  re-ranks. The response carries **two already-ranked collections**, each with its own correct
  `rank`:
  - `rankings.grossProfit` — ranked by Gross Profit desc
  - `rankings.averageCheck` — ranked by Average Check desc
  React only switches **which pre-ranked collection is displayed**.
- Each row carries rank, manager, Paid Sales, Revenue, Gross Profit, Average Check, Margin, and Δ vs.
  previous period.
- **Eligibility:** any manager with **≥ 1 Paid sale in the window** is ranked, regardless of the
  manager's current `active` flag (inactive managers with historical Paid sales stay eligible for
  historical rankings). Managers with 0 Paid sales in the window are omitted; an empty ranking shows
  the empty-state.
- **Deterministic tie order** (documented): by the selected metric desc, then Gross Profit desc, then
  Revenue desc, then manager name asc, then manager id asc. Equal metric values receive sequential
  ranks in that stable order. `rank` is assigned server-side in exactly this order.

## 5. Period & timezone semantics

- **Storage:** all Sale timestamps are UTC in PostgreSQL `timestamptz`.
- **Reporting timezone:** one configured offset, default **UTC+03:00 (MSK)** — a fixed offset (MSK
  observes no DST, so no ambiguity). Calendar periods are computed in this offset, then converted to
  UTC instants for querying. A DST-observing region would need full IANA handling (evolution path).
- **Windows are half-open `[start, end)`** UTC instants: `start` inclusive, `end` exclusive. A sale
  exactly at `end` belongs to the next window — this removes every day/month off-by-one.
- **Custom range:** the UI sends **date-only** inclusive calendar dates `from`/`to`; the server
  resolves them to `[from 00:00 MSK → UTC, (to+1 day) 00:00 MSK → UTC)`.
- **Injectable clock:** `now` is captured **exactly once per request** from an injected
  `TimeProvider` (UTC), then all boundaries are derived from that single instant, so period logic is
  testable and internally consistent.

### Exact previous-period per preset (frozen — NOT one arithmetic rule)

Let `nowMsk` = captured now in MSK, `dayStart(d)` / `monthStart(d)` = MSK calendar boundaries. All
windows are `[start, end)`, converted to UTC for SQL.

| Preset | Current `[start, end)` | Previous `[start, end)` |
|---|---|---|
| **Today** | `[dayStart(today), now)` (day-to-date) | `[dayStart(yesterday), dayStart(yesterday) + elapsed)` where `elapsed = now − dayStart(today)` |
| **Last 7 days** | `[dayStart(today − 6d), now)` | current shifted back **7 calendar days**: `[dayStart(today − 13d), now − 7d)` |
| **Last 30 days** | `[dayStart(today − 29d), now)` | current shifted back **30 calendar days**: `[dayStart(today − 59d), now − 30d)` |
| **This month** | `[monthStart(this), now)` (month-to-date) | `[monthStart(prev), min(monthStart(prev) + elapsed, monthStart(this)))` where `elapsed = now − monthStart(this)` — **capped at the end of the previous month** when it is shorter (Feb/Mar case has a test) |
| **Previous month** | `[monthStart(prev), monthStart(this))` (full month) | `[monthStart(prev−1), monthStart(prev))` (the full month before) |
| **Custom** | `[dayStart(from), dayStart(to + 1d))` from inclusive date-only input | immediately preceding equal-duration range: `[start − L, start)` where `L = end − start` |

The response **echoes both resolved UTC windows** (current `{start,end}` and previous `{start,end}`)
so the comparison is transparent and testable. Tests cover every preset, exact start/end boundary
inclusion, and the This-month shorter-previous-month cap.

## 6. One-sale analytical grain — CRITICAL invariant

**Never count joined SaleItem rows as sales; never let item count inflate Paid Sales or Average
Check.** Every analytical query conceptually does:

```
Sale → aggregate its SaleItems into sale-level revenue & cost
     → apply status semantics (Paid contributes; Cancelled/Refunded contribute 0, excluded from count)
     → aggregate those one-row-per-Sale facts into dashboard metrics
```

A Paid sale with 5 items is **one** Paid sale contributing one revenue/cost figure. This is proven
by a dedicated test (a multi-item Paid sale must not inflate Paid Sales or Average Check).

## 7. Presentation semantics (frontend formats; backend still owns the numbers)

- **Currency:** one fixed demo currency, **USD ($)**, across the whole dataset. Money stored as
  `numeric(18,2)` (headroom for the "very large transaction" edge case), handled as `decimal` server-side.
- **Money formatting:** tables → `$1,234.56` (grouping, 2 dp); KPI cards → compact `$1.23M` / `$12.3K`.
- **Percentages:** Margin to **1 decimal** (`42.3%`). Percentage deltas 1 decimal. Margin delta in
  **percentage points** (`+2.1 pp`).
- **Trend buckets:** the API returns a **continuous** series (zero-filled empty buckets) so a gap
  never silently looks like missing data. Auto-granularity by window length (≤2d hourly, ≤~92d daily,
  ≤~730d weekly, else monthly) — echoed in the response.
- **Recent Sales:** shows each sale's **original** amount / cost / gross profit with an obvious
  status badge (non-color-only), so Cancelled/Refunded rows visibly do **not** reconcile with the
  net financial KPI totals. Status distinction must be unmistakable.

## 8. Zero / empty behavior

Empty window → additive metrics 0, Paid Sales 0, Margin & Average Check `null`, Best Manager `null`,
ranking/trend/category/product blocks render their empty-states. Every division guards its
denominator in both API and UI.

## 9. Deterministic seed intent

Fixed RNG seed + one captured anchor date + transactional, idempotent execution (a seed/version
marker prevents duplication on re-run). Scale: 15–25 managers, 50–100 customers, several categories,
several dozen products, 2,000–5,000 sales over ~12 months. Non-uniform: strong/weak managers, varied
average checks & margins, seasonality, cancellations & refunds, a few very large sales among many
small, and managers with sales gaps. Dates anchored to the seed date so trailing windows always have
data on a fresh clone.
