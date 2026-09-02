# Implementation Plan — Sales Performance Dashboard (DJI-Market take-home)

Status: **ARCHITECTURE FROZEN (2026-09-02, final correction). Implementation phase.**
Timebox: ~8 hours. Priority discipline: never sacrifice P0 for P2.
Business/date semantics live in [`.claude/skills/sales-domain`](../.claude/skills/sales-domain/SKILL.md)
— the single source of truth. This plan does not restate them; it references them.

Guiding principle: **the simplest architecture that preserves good boundaries and leaves a credible
path for evolution.** No further redesign after this.

---

## 1. Architecture — frozen lightweight modular monolith

One deployable API, one PostgreSQL database, one frontend. Modules are separate class-library
projects (boundaries enforced by the reference graph). **No further layers/projects will be added.**

```
backend/
  src/
    SalesDashboard.Api             host: minimal-API endpoint(s), startup migrate+seed, /api/health/ready, DI
    SalesDashboard.Contracts       STABLE vocabulary ONLY: SaleStatus, dashboard request/response DTOs,
                                   small shared value types. No infrastructure plumbing.
    SalesDashboard.Infrastructure  WriteDbContext (aggregates module EF configs), single migration history,
                                   transactional deterministic seeder, TimeProvider + NpgsqlDataSource registration
    Modules/
      SalesDashboard.Modules.Sales     Sale, SaleItem + EF configs + status rules   (schema: sales)
      SalesDashboard.Modules.Catalog   Product, Category + EF configs               (schema: catalog)
      SalesDashboard.Modules.People    Manager, Customer + EF configs               (schema: people)
      SalesDashboard.Modules.Analytics period resolver + analytical reads. Depends directly on Npgsql/Dapper
                                       (+ Contracts for DTOs). Returns dashboard DTOs, never persistence entities.
  tests/
    SalesDashboard.Tests           xUnit: period math (pure) + analytics (one shared Testcontainers Postgres)
frontend/                          Vite + React 19 + TS + TanStack Query + Tailwind + Recharts (NO Framer Motion)
docker-compose.yml  README.md  AI_PROMPTS.md  AI_NOTES.md  docs/PLAN.md  .claude/{agents,skills}
```

**Changes from the pre-freeze plan (this correction):**
- **SignalR removed from core** — no `DashboardHub`, no sample-sale mutation endpoint/button, no
  notifier/dispatcher, no websocket/nginx complexity, no realtime invalidation. Documented in README
  as *considered but deliberately rejected* (a realtime transport with no genuine event source adds
  failure surface without improving mandatory functionality). Revisit only after everything mandatory
  is done, tested, Docker-verified, documented, with substantial time left.
- **`Contracts` shrunk** to stable vocabulary; no bespoke connection abstraction — Analytics uses the
  `NpgsqlDataSource`/Dapper primitives directly; `INotifier`/event abstractions **deleted from scope**
  (their only consumer was SignalR).
- **Six read endpoints → one `GET /api/dashboard`** (see §4).

### Relational integrity (restored)
One `WriteDbContext`, one migration history, but **real PostgreSQL foreign keys across schemas**:
`catalog.products → catalog.categories`, `sales.sales → people.managers`,
`sales.sales → people.customers`, `sales.sale_items → sales.sales`,
`sales.sale_items → catalog.products`. Delete behavior **restrict** (no silent cascade of history).
Constraints: `quantity > 0`, `unit_price >= 0`, `unit_cost >= 0`, `base_price >= 0`, `base_cost >= 0`
(CHECK); money `numeric(18,2)`; sale timestamps `timestamptz` (UTC); one-row `ops.seed_state` marker
for idempotent seeding.

---

## 2. Business & date semantics — see `sales-domain` (frozen)
Paid/Cancelled/Refunded contribution, the metric formulas, null-vs-0 ratio rules, "Paid Sales"
terminology, the one-sale grain invariant, half-open UTC windows, the **semantic per-preset**
previous-period table, reporting timezone (fixed **UTC+03:00 MSK** offset), and presentation
formatting (USD, rounding, pp deltas) are all fixed in the skill. Do not reinterpret them here.

---

## 3. Persistence & analytics approach
- **Write side:** EF Core, module-owned `IEntityTypeConfiguration`s scanned into one `WriteDbContext`
  in `Infrastructure`. Single `MigrateAsync` on startup.
- **Read side (Analytics):** depends directly on **Npgsql + Dapper** (no extra abstraction) and on
  Contracts for the DTOs it returns. **EF Core is write/migration/seed only — Analytics does not use
  it.** All analytical reads are Dapper/raw SQL (grouped financial aggregation, time bucketing,
  ranking/window functions). **No SQL views/adapter projects** merely to purify the dependency graph.
  README states honestly: Analytics SQL is coupled to the PostgreSQL **schema**, though not to EF
  entity types. Ranking `rank` and tie-breaking are computed server-side.
- **One-sale grain** enforced in every query (aggregate items → sale fact → status → dashboard),
  proven by a dedicated multi-item test.
- **No isolation-level rule.** The seeded dataset is effectively static during evaluation; the request
  resolves the period once and reads normally. `REPEATABLE READ` is **not** an architectural rule —
  add isolation/transaction complexity only if the implementation genuinely proves it necessary.

---

## 4. API surface (composed, aggregated, ProblemDetails errors)

```
GET /api/dashboard?preset=today|last7|last30|thisMonth|prevMonth   (or)  ?from=YYYY-MM-DD&to=YYYY-MM-DD
GET /api/health/ready        readiness: DB reachable AND migrated AND seeded
```

`GET /api/dashboard` returns **one cohesive snapshot** (not a raw-record dump):
```
{ period:   { preset, current:{start,end}, previous:{start,end}, timezone, granularity },
  summary:  { revenue, cost, grossProfit, paidSales, margin|null, averageCheck|null,
              bestManager|null, previous:{…}, deltas:{…, marginDeltaPp|null, …} },
  rankings: { grossProfit:  [ { rank, managerId, name, initials, active, paidSales, revenue,
                                grossProfit, averageCheck|null, margin|null, deltas:{…} } ],
              averageCheck: [ { rank, … same shape, ranked by Average Check } ] },  // server-ranked, both
  trend:    [ { bucketStart, revenue, grossProfit, paidSales } ],  // continuous, zero-filled
  categories:[ { categoryId, name, revenue, grossProfit, share } ],
  topProducts:[ { productId, name, category, revenue, grossProfit, unitsSold } ],
  recentSales:[ { saleId, occurredAt, manager, customer, itemsSummary, status, amount, cost, grossProfit } ] }
```
Invalid dates / `from > to` / unknown preset → **400 ProblemDetails**. The response ships **both
server-ranked** collections; the frontend only switches which one it displays (no re-rank, no extra
endpoint). Additional endpoints only if a concrete interaction needs independent retrieval — none yet.

---

## 5. Frontend (simplified)
React 19 + TS (strict) + Vite + TanStack Query + Tailwind + **Recharts**. **No Framer Motion** —
CSS/Tailwind transitions only. One `GET /api/dashboard` per period keyed in TanStack Query;
`keepPreviousData` drives the "Updating…" refetch state. Layout for 1440×900. Every block honors the
five async states and the accessibility/formatting bar in
[`frontend-quality`](../.claude/skills/frontend-quality/SKILL.md). No extra charts until the polished
minimum is excellent.

---

## 6. Docker — early milestone (not the final hour)
Root `docker-compose.yml`: `db` (postgres:17-alpine, `pg_isready` healthcheck, named volume, host
port unpublished), `api` (multi-stage build; startup migrate + **transactional seed**; readiness at
`/api/health/ready` reflecting **migration + seed complete**, not just a live port; `depends_on db
healthy`), `web` (node build → nginx serving the SPA and proxying `/api`; `depends_on api healthy`;
the only published service → `http://localhost:8080`). Verified from a clean state as soon as the
minimal DB/API/web slice exists, and again with clean volumes before submission, per
[`docker-verification`](../.claude/skills/docker-verification/SKILL.md).

---

## 7. Seed correctness
Fixed RNG seed · one captured anchor date · **transactional** execution · an explicit
**seed/version marker** row so re-running never duplicates. Readiness stays false until migrations +
seed succeed. Scale and non-uniform distribution + edge cases per `sales-domain` §9.

---

## 8. Testing — focused (one shared Testcontainers Postgres)
One shared PG Testcontainer fixture + a small handcrafted dataset: a **multi-item Paid sale**, a
Cancelled sale, a Refunded sale, a manager with no sales, exact start & end boundary sales, a ranking
tie, a zero-revenue period. Highest-value assertions: **sale count not inflated by SaleItems**,
summary financials, status treatment, date boundaries, previous period (per preset), ranking/tie
behavior, and **trend and category totals reconciling with summary** (limited top-N products do not
reconcile — assert their per-row correctness and ordering instead). Pure unit tests for the period
resolver via a fake `TimeProvider`. Frontend (Vitest + Testing Library): period switch, ranking-mode
switch, loading, error. Not chasing coverage.

---

## 9. AI-native workflow (evidence, not ceremony)
Roles that are actually used: **survey → implementation → independent review → UI/browser review
(when there is running UI)** — `.claude/agents/*`. Authoritative/consumed skills: `sales-domain`,
`analytics-verification`, `docker-verification`, `frontend-quality`. Workflow per task: requirements
→ survey → implement → **DB verification (psql ground truth)** → **browser verification** →
independent review → fixes → final validation. A clean compile / green tests are necessary, not
sufficient. No PostgreSQL MCP exists in this environment; `psql` in the container provides the
equivalent ground-truth check.

---

## 10. Git milestones (meaningful, no padding)
1. initial project structure + docs + AI logs + agents/skills
2. domain modules + WriteDbContext + FKs/constraints + initial migration
3. deterministic transactional seed
4. **early Docker vertical slice** (db+api+web) verified clean
5. analytics module + `GET /api/dashboard` (period resolver, one-sale grain, semantic previous)
6. React dashboard shell + KPI cards + period controls
7. trend chart + ranking switch + categories + top products + recent sales
8. required async states (skeleton / updating / error+retry / empty) + formatting/a11y
9. backend (Testcontainers) + frontend (Vitest) tests
10. final clean-volume Docker verification + README + AI_NOTES + review pass

---

## 11. Revised priority
- **P0:** frozen business/date contracts · relational schema integrity · deterministic transactional
  seed · early clean Docker vertical slice · coherent `/api/dashboard` · correct PostgreSQL analytics
  · **previous-period comparison on the summary KPIs** (the calculation already exists and is high
  product value) · required React dashboard · required states · focused tests · README/AI docs ·
  final clean Docker verification.
- **P1:** visual polish · **per-manager historical deltas** (if time becomes constrained) · richer
  category/product presentation.
- **P2 (only if substantial time remains):** additional analytics · Playwright · SignalR.

---

## 12. Locked decisions
Folder `E:\Work\GitProjects\SalesDashboard` · .NET 10 · web on host `8080` · currency **USD** ·
reporting offset **UTC+03:00** · Testcontainers for backend query tests · single `WriteDbContext` /
single migration history · single `GET /api/dashboard` endpoint · **server-ranked** dual collections
(grossProfit + averageCheck) · Analytics reads via **NpgsqlDataSource/Dapper directly** · **no
SignalR / no outbox / no Framer Motion / no SQL views / no forced REPEATABLE READ** in core scope.
