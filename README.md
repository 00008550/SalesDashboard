# Sales Performance Dashboard

A small full-stack sales-analytics dashboard: a manager can pick a period and immediately see
revenue, profit, margin, the best performers, category and product breakdowns, a time-series trend,
and the most recent sales — all computed server-side over a realistic seeded dataset.

**Stack:** .NET 10 · ASP.NET Core (minimal APIs) · EF Core + Dapper · PostgreSQL 17 · React 19 +
TypeScript · TanStack Query · Tailwind · Recharts · Docker Compose.

---

## Running it

```bash
docker compose up --build
```

Then open **http://localhost:8080**. On first start the API applies EF Core migrations and runs the
deterministic seed automatically; the dashboard loads populated. No manual database creation, SQL,
migration, or seed step is needed. (The database and API ports are internal; nginx serves the SPA
and proxies `/api` to the API, so the browser only ever talks to one origin.)

`docker compose up --build` is the evaluator-friendly one-command start, but Docker may reuse a
locally cached base-image manifest and build layers. For a deliberately clean database and freshly
pulled bases, use the following instead; **the first command deletes this project's local database
volume**:

```bash
docker compose down --volumes --remove-orphans
docker compose build --pull --no-cache
docker compose up
```

The API and web runtime stages also request patched Alpine OpenSSL packages (and libexpat for web)
during their builds. After building, the exact high/critical image checks are:

```bash
docker scout cves --only-severity critical,high salesdashboard-api:latest
docker scout cves --only-severity critical,high salesdashboard-web:latest
```

To reproduce the local build, test, audit, and formatting checks from the repository root (the
backend tests need a running Docker daemon because they use PostgreSQL through Testcontainers):

```bash
# Backend
dotnet build backend/SalesDashboard.slnx -c Release
dotnet test backend/SalesDashboard.slnx -c Release --no-build
dotnet format backend/SalesDashboard.slnx --verify-no-changes

# Frontend
npm --prefix frontend ci
npm --prefix frontend audit
npm --prefix frontend test
npm --prefix frontend run build
```

---

## Business rules (the authoritative definitions)

The **backend is the single source of truth** for every calculation; the frontend only formats. All
figures are computed at the **one-sale grain**: line items are rolled up to a per-sale fact first,
sale status is applied, then sale-level facts are aggregated — so a Paid sale with five items is one
Paid sale and never inflates counts or averages.

| Metric | Definition |
|---|---|
| **Revenue** | Σ (sale price × quantity) over **Paid** sales. Default 0. |
| **Cost** | Σ (unit cost × quantity) over **Paid** sales. Default 0. |
| **Gross Profit** | Revenue − Cost. |
| **Margin** | Gross Profit / Revenue — **null** when Revenue = 0 (never NaN/Infinity). |
| **Paid Sales** | Count of **Paid** sales (the Average Check denominator; always called "Paid Sales", never bare "Sales"). |
| **Average Check** | Revenue / Paid Sales — **null** when Paid Sales = 0. |
| **Best Manager** | Highest Gross Profit in the period — null when no manager has Paid sales. |

**Status handling**

- **Paid** — contributes to revenue, cost, gross profit and the Paid-Sales count.
- **Cancelled** — contributes **nothing** to any financial aggregate and is excluded from the count.
- **Refunded** — contributes **net zero**; excluded from the count. Both Cancelled and Refunded
  remain **operationally visible** in Recent Sales (with a clear, non-colour-only badge and their
  **original** amounts), so those rows visibly do *not* reconcile with the net KPI totals.

  *Refunded — documented simplification:* without refund timestamps or a ledger, a refund removes the
  sale from the financial aggregates of its **original** sale period rather than creating a
  later-period reversal. The production evolution is a refund ledger with its own event date.

**Ranking.** Two mandatory modes, **Gross Profit** and **Average Check**, are both ranked
**server-side** and returned as two pre-ranked collections; the client only switches which one it
displays. A manager with ≥ 1 Paid sale is ranked regardless of their active flag (inactive managers
with historical sales stay eligible). Deterministic tie-break: selected metric desc → Gross Profit
desc → Revenue desc → name asc → id asc.

**Previous-period comparison — semantic per preset** (not one arithmetic rule). Windows are half-open
`[start, end)` UTC instants; `now` is captured once per request; calendar boundaries are resolved in
the reporting timezone (**fixed UTC+03:00 / MSK** — MSK has no DST, so a fixed offset is exact) and
converted to UTC. Custom ranges take inclusive date-only `from`/`to` and resolve to
`[from 00:00, (to+1) 00:00)`. A custom range may contain at most **731 inclusive calendar days**
(`to - from + 1 <= 731`); the UI validates this before requesting and the API enforces the same cap.

| Preset | Current | Previous |
|---|---|---|
| Today | day-to-date | previous day, same elapsed time |
| Last 7 days | last 7 calendar days incl. today | the same window shifted back 7 days |
| Last 30 days | last 30 calendar days incl. today | shifted back 30 days |
| This month | month-to-date | same elapsed portion of the previous month, **capped** when it is shorter (e.g. Mar 31 → all of Feb) |
| Previous month | the full previous calendar month | the month before it |
| Custom | `[from, to]` | the immediately preceding window of equal duration |

The API echoes both resolved UTC windows in every response. Percentage deltas are **null** when the
previous baseline is 0 (shown as "New"); margin deltas are reported in **percentage points**.

---

## Architecture

A **lightweight modular monolith** — one deployable API, one PostgreSQL database, one frontend. The
modules are separate class-library projects so the boundaries are enforced by the project-reference
graph, but each stays small with no abstraction that doesn't earn its place.

```
backend/src/
  SalesDashboard.Api             minimal-API host, startup migrate+seed, /api/health/ready, DI
  SalesDashboard.Contracts       stable vocabulary: SaleStatus + dashboard request/response DTOs
  SalesDashboard.Infrastructure  WriteDbContext (one migration history), transactional seeder, NpgsqlDataSource
  Modules/Sales | Catalog | People   domain entities + EF configs (schemas: sales / catalog / people)
  Modules/Analytics              PeriodResolver + Dapper read queries -> Contract DTOs
frontend/                        React 19 + TS + TanStack Query + Tailwind + Recharts, served by nginx
```

- **EF Core is write/migration/seed only.** Cross-module foreign keys live in `WriteDbContext` (the
  one place that sees every module's types), so module isolation in code is preserved while
  PostgreSQL enforces integrity across schemas.
- **Analytics reads with Dapper/Npgsql** — analytical aggregation is where hand-written SQL is
  clearest, and it decouples the read model from the write entities. It is honestly coupled to the
  database **schema** (column/table names), not to EF types; there are no SQL views or adapter
  projects added purely to purify the graph.
- **One composed endpoint.** `GET /api/dashboard` returns a single coherent snapshot (period resolved
  once) rather than six chatty calls, and it is not a raw-record dump — every block is pre-aggregated.

**Considered but deliberately rejected for this scope** (would add failure surface without improving
a mandatory requirement): SignalR/real-time (there is no genuine event source here — a fabricated
"sample sale" is not one), a transactional outbox (no distributed message boundary), Kafka/RabbitMQ,
Redis, Clean Architecture / CQRS / MediatR / generic repositories, and per-module DbContexts.

## API

| Endpoint | Purpose |
|---|---|
| `GET /api/dashboard?preset=today\|last7\|last30\|thisMonth\|prevMonth` **or** `?from=YYYY-MM-DD&to=YYYY-MM-DD` | The full dashboard snapshot: resolved period, summary KPIs with previous-period deltas, two server-ranked manager collections, zero-filled trend, categories, top products, recent sales. Invalid dates, `from > to`, ranges over 731 inclusive days, mixed preset/date parameters, and unknown presets → **400 ProblemDetails**. |
| `GET /api/health/ready` | Readiness — 200 only after migrations and seed/repair complete **and** the pooled dashboard read path is usable (drives the Docker health check). |
| `GET /api/health/live` | Liveness. |

## Database

Entities: **Manager, Customer** (schema `people`), **Category, Product** (`catalog`),
**Sale, SaleItem** (`sales`). Money is `numeric(18,2)`; sale timestamps are `timestamptz` (UTC).

- **Foreign keys** across schemas, all `ON DELETE RESTRICT`: product→category, sale→manager,
  sale→customer, sale_item→sale, sale_item→product.
- **Check constraints:** `quantity > 0`; `unit_price`, `unit_cost`, `base_price`, `base_cost` ≥ 0.
- **Indexes:** a composite `(status, occurred_at)` on sales for the hot filtering path
  (Paid + date range), plus `occurred_at`, `manager_id`, and the FK columns.
- **Seed:** deterministic (fixed RNG) so the *shape* is identical on every rebuild — 18 managers
  (incl. inactive ones with history and blackout gaps), 70 customers, 6 categories, ~40 products,
  ~3,200 sales over ~12 months, with seasonality, varied margins/average checks, a mix of large and
  small deals, and Cancelled/Refunded sales. It runs in one transaction and records a version marker
  (`ops.seed_state`), so re-running never duplicates data and readiness stays false until it succeeds.

## Testing

Meaningful behaviour over coverage. **Backend (xUnit + Testcontainers PostgreSQL):** full
`PeriodResolver` coverage incl. the Feb/March shorter-month cap and custom boundaries; analytics over
a hand-crafted dataset — one-sale grain, status treatment, exact start/end boundaries, previous
period, ranking + tie-break, trend/category reconciliation with the summary, and the zero-revenue
period; a deterministic-seed idempotency test; and a `WebApplicationFactory` startup test proving
migrate→seed→ready + populated data. **Frontend (Vitest + Testing Library):** loading→loaded,
ranking-mode switch, period-switch refetch, error + Retry, and the empty-period states.

## Trade-offs & decisions

- **One aggregated write DbContext** (single migration history → one `MigrateAsync` at startup) over
  per-module contexts — simpler for the auto-migrate requirement; module ownership is by discipline.
- **Fixed-offset reporting timezone** (MSK) rather than full IANA/DST handling — exact for MSK and
  dependency-free; a DST region would need `TimeZoneInfo`.
- **Deterministic shape, floating dates:** the RNG seed is fixed but sale dates anchor to the seed
  moment, so a fresh clone always has recent data in every preset. Absolute dates shift with the
  build date; the distribution does not.

## Timebox boundary and production follow-ups

No mandatory feature was knowingly left half-implemented. The work intentionally left outside the
take-home timebox is production hardening: real authN/Z and tenant scoping (authentication was
explicitly out of scope); pagination and server-side sorting for a larger sales feed; caching or a
materialized/background read model for heavier datasets; traces and metrics on query paths; a refund
ledger with event dates; CI for the Testcontainers/frontend suites; and bundle splitting (Recharts is
the largest chunk). The current fixed-size Recent Sales page and original-period refund
simplification are deliberate documented limits, not hidden unfinished paths.

## AI-assisted workflow

**Claude Code (Opus 4.8)** was the primary implementation agent. **OpenAI Codex** then acted as an
independent read-only PR reviewer/verifier and found corrective issues that were addressed in later
explicitly authorized passes, including this final Codex implementation and re-verification pass.
Mechanical EF/seed/SQL/React/test work was delegated; business/date
semantics, architecture and scope were manually owned and challenged with psql reconciliation, real
database upgrade/restart scenarios, measured 1440x900 browser states, keyboard and axe checks, and
container scans. The `.claude/` folder records the implementation roles and skills. See
[`AI_PROMPTS.md`](AI_PROMPTS.md) for the prompt log and [`AI_NOTES.md`](AI_NOTES.md) for the concise
reflection, including rejected suggestions and genuine AI errors.
