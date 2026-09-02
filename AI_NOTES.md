# AI_NOTES

Reflection on how AI was used to build this project. Kept short and honest. Filled in as work
proceeds; finalized at the end.

## Tooling
- **Claude Code (Opus 4.8)** — primary coding agent, used for survey, planning, implementation,
  and self-review.
- **Verification tooling (used as work reaches each stage — recorded here as it actually happens):**
  `psql` inside the `db` container (schema + analytical-query ground-truth, cross-checked against the
  API JSON) and the built-in **Browser pane** (run the app, switch periods, switch the displayed
  ranking, confirm loading/error/empty states). No PostgreSQL MCP was available in this environment,
  so `psql` provides the equivalent database-verification value.

## Workflow (intended; each agent/skill is noted below only once it is actually used)
requirements → survey → architecture → implementation → **database verification** →
**browser verification** → independent review → fixes → final validation. A clean compile and green
tests are treated as necessary, not sufficient.

## What I delegated vs. designed myself
- **Delegated to the agent:** the bulk of the mechanical implementation — EF configurations and the
  migration, the deterministic seed generator, the Dapper analytical SQL, the React components and
  Tailwind styling, and both test suites.
- **I owned the design and every judgement call:** the modular boundaries and what to reject
  (SignalR, outbox, CQRS), the frozen business/date semantics, the one-sale-grain invariant, the
  single composed endpoint, server-side ranking, the seed strategy, and the Docker topology. I drove
  the survey → review → fix loop and verified results against ground truth rather than trusting them.
- Design decisions I own and must be able to defend: the **lightweight modular-monolith boundaries**
  (Sales / Catalog / People / Analytics, Analytics reading via `Contracts` with selective Dapper),
  the single-write-context + single-migration-history trade-off, the **one-sale analytical grain**
  invariant, the Refunded simplification, the **semantic per-preset** previous-period rules, the
  fixed reporting offset (MSK) with `timestamptz` storage, the deterministic transactional seed, and
  the single composed `GET /api/dashboard`.

## Findings from independent review (to expand at the end — recorded honestly, not invented)
An independent review of the approved plan surfaced real issues that changed the design **before**
implementation:
- **Conflicting null/zero ratio semantics** — Margin/Average Check/percentage-delta needed one frozen
  rule (null when the denominator/baseline is 0; never NaN/Infinity). Fixed in `sales-domain`.
- **Multi-SaleItem counting risk** — naive joins would count SaleItem rows as sales and inflate Paid
  Sales / Average Check. Fixed by the one-sale-grain invariant + a dedicated test.
- **Date-period ambiguity** — one arithmetic previous-period rule was wrong for presets; replaced with
  semantic per-preset comparisons and an explicit reporting timezone + injectable clock.
- **SignalR rejected after review** — it depended on an invented sample-sale mutation, not a real
  event source; removed from core scope and documented as a deliberate rejection.
- **Dashboard endpoint simplification** — six independent read endpoints collapsed into one coherent
  `GET /api/dashboard` response (the period is resolved once per request). Note: there is **no**
  wrapping transaction/isolation level — the seeded dataset is static during evaluation, so a
  "consistent DB snapshot" is not claimed.

## Where AI accelerated the work
- Scaffolding (7 backend projects + the Vite app), the boilerplate-heavy EF configs and migration,
  the seed generator, the six analytical queries with their trend bucketing, and the React component
  set — all produced quickly, leaving my time for the semantics, boundaries, and verification.
- Fast, honest debugging of the genuine bugs listed above once tests/psql/Docker exposed them.

## Where AI got it wrong / what I changed or rejected (genuine, caught during implementation)
- **Stale initial migration**: the first-pass migration had only 1 of 5 FKs (as CASCADE) and no CHECK
  constraints. Corrected the EF configs (cross-module FKs in WriteDbContext, all RESTRICT) + checks
  and regenerated the migration.
- **Dockerfile `adduser` collision**: the .NET 10 Alpine image already ships a non-root `app` user, so
  `adduser -D -u 64198 app` failed the build. Fixed by reusing the built-in user.
- **Startup integration test config**: injecting the connection string via the factory's
  `ConfigureAppConfiguration` applied too late (it's read during `CreateBuilder`); switched to the
  environment variable the app already reads.
- **Npgsql UTC requirement**: Npgsql maps `timestamptz` to a `DateTimeOffset` whose offset must be
  zero; period boundaries were being computed in the +03:00 reporting offset and passed straight to
  SQL, which Npgsql rejects. Fixed by converting resolved boundaries to UTC (`ToUniversalTime`) before
  querying, while still resolving the calendar in MSK.
- **Lowercase-PK column casing**: the entity configs mapped primary keys as `Id`, but the rest of the
  schema (and the analytical SQL) uses snake_case `id`. The mismatch surfaced as PostgreSQL `42703`
  (column does not exist) at read time. Fixed by renaming the key columns to `id` through the additive
  `RenameKeyColumnsToId` migration rather than editing the committed `InitialSchema`.

## How generated code was verified (as of the seed/startup milestone)
- `dotnet build` green (0 warnings); **4 focused backend tests pass** on a real Testcontainers
  PostgreSQL (seed scale + edge cases, idempotency, deterministic shape, WebApplicationFactory
  startup → ready + populated).
- **`docker compose up --build` from a clean state**: db → api health-ordered, `/api/health/ready`
  200, `/api/meta/counts` **reconciled against psql ground truth** (sales 3200, sale_items 5419,
  seed v1); api restart idempotent ("Seed v1 already applied … skipping"). Not taken on trust — the
  API numbers were cross-checked against SQL, and the restart proved idempotency.

## PR #1 review — findings fixed and how they were verified
An independent review of the PR caught real defects; all were fixed on the PR branch (no history
rewrite) and verified:
- **Migration lineage** — the PR had replaced the committed InitialSchema, which would break an
  upgrade from `master`. Restored the original migration and moved the id-column change into an
  additive `RenameKeyColumnsToId`. Verified an actual upgrade: a database seeded from `origin/master`
  (history `…041021`, `Id` columns, 3200 sales) was brought up on PR HEAD — it applied only the
  rename, kept **3200** sales, and the columns became `id`; a fresh clean start applies both.
- **Future-dated seed rows** — the seeder could place a sale after the anchor. Fixed to clamp within
  the MSK day and ≤ anchor; verified in the running DB (`future_dated_sales = 0`).
- **Trend bucketing** included the half-open end bucket — fixed; tests assert exact bucket counts.
- **Reporting-timezone dates** were formatted as UTC — fixed to MSK; formatter tests added.
- **Latched readiness** returned 200 after Postgres stopped — replaced with a live check (DB
  reachable + seed version present); a test stops a container and asserts 503.
- **Summary contract** — added Cost; margin delta now in real percentage points (frontend no longer
  multiplies).
- **Accessibility** — verified with an **axe-core WCAG A/AA scan in the running app: 0 violations**
  (fixed low-contrast toggle text on the slate-100 group, the "inactive" badge, and darkened the
  avatar palette so white initials pass; the scrolling ranking list is keyboard-focusable).
- **Contract/docs** — mixed preset+dates now 400; `/api/meta/counts` removed; stray README fence and
  the false "consistent snapshot" note removed; `*.tsbuildinfo` ignored.

**Verification at the first-review round:** backend `dotnet build` 0 warnings, **22/22 backend tests**
pass (Testcontainers PostgreSQL); **9/9 frontend tests** pass; production frontend build clean; fresh
`docker compose up --build` all three tiers healthy; the master→PR upgrade applied only the additive
rename and preserved the 3200 sales; seed restart idempotent; independent psql reconciliation of
**Revenue, Cost, Gross Profit and Paid Sales** matched the API exactly; browser pass at 1440×900
(render, ranking swap, empty state) with a clean console and 0 axe violations.

## PR #1 second re-review — findings addressed and how they were verified

A second independent re-review of the PR at HEAD `3184b60` found that the first round had left real
gaps. These were fixed with additive commits on the branch (no history rewrite, PR not merged) and
verified. This section records only verification actually performed.

- **Upgrade repair for already-seeded databases (finding 1).** The first-round seed fix corrected only
  *fresh* databases: `SeedVersion` stayed `1`, so a database seeded by the pre-fix generator kept its
  future-dated rows on upgrade. Added a non-destructive, idempotent repair in the seeder that runs on
  the same-version path and pulls only rows with `occurred_at > applied_at` back into the anchor's MSK
  day (placement derived from the sale id, so it is stable), touching no ids and no counts.
  **Verified with a real image-to-image upgrade:** the `origin/master` API image seeded a database to
  3200 sales / 5419 items with **12 future-dated rows**; bringing that same database up on the HEAD
  image applied `RenameKeyColumnsToId`, ran the repair, and left 3200 sales / 5419 items with **0
  future-dated rows** and an **identical md5 of the sorted sale-id set** (ids preserved). A
  Testcontainers regression (`Upgrade_repairs_future_dated_sales_preserving_ids_and_counts`) asserts
  the same on a seeded-then-perturbed v1 database.
- **Readiness proves the latest migration (finding 2).** Connectivity plus a v1 seed marker was
  insufficient — that marker predates `RenameKeyColumnsToId`, so a database reverted to `InitialSchema`
  answered 200 while `/api/dashboard` failed with `42703`. Readiness now also requires an empty pending
  migration set (`ReadinessProbe`). Regression `Reachable_seeded_database_missing_the_latest_migration_is_not_ready`
  migrates a reachable, seeded database only to `InitialSchema` and asserts **503 `migrations_pending`**,
  then applies the rename and asserts **200 `ready`**.
- **Frontend correctness and accessibility (finding 3).** Compact money is now a frozen 3-significant-
  figure format (`$1.23M`, `$12.3K`) with formatter tests; trend colours are blue-700 / emerald-700 /
  amber-700, all ≥4.5:1 on white (legend text passes AA, strokes clear the 3:1 non-text minimum); the
  ranking switch is ordinary grouped buttons with `aria-pressed` (no half-built tablist); the ranking
  list fills the card height (`flex-1 min-h-0`) instead of a fixed `max-h`; the error card is a
  `role="alert"` live region; focus rings are blue-600 (≥3:1). **Verified in the running app at
  1440×900: an axe-core WCAG 2 A/AA scan reported 0 violations (0 colour-contrast), console clean.**
- **Tests and isolation (finding 4).** The Retry test now clicks Retry and proves a fresh request plus
  successful recovery; a deferred-request test proves the prior snapshot is retained, `Updating` and
  `aria-busy` show while pending and clear on completion. `ReadinessTests` joined the non-parallel
  `postgres` collection so it no longer races `StartupTests` over the process-global
  `ConnectionStrings__Default`.
- **Bounded custom periods (finding 5).** `ResolveCustom` rejects ranges beyond `MaxCustomRangeDays`
  (731) with an `ArgumentException` that the endpoint renders as **400 ProblemDetails**; verified live
  (`from=2000-01-01&to=2026-01-01` → 400) and with two resolver tests (at the max, and one past it).
- **Refreshed runtime images (finding 6).** The frozen `nginx:1.27-alpine` runtime was moved to the
  current `nginx:1.31-alpine` (alpine 3.24); the .NET 10 and Postgres 17 alpine bases were re-pulled.
  Docker Scout on the freshly built images: web dropped from 6C/24H to **2C/9H**, api **2C/7H**. The
  remaining critical/high findings are entirely in upstream base packages — `openssl 3.5.7-r0` (both
  images) and `expat 2.8.2-r0` (web) — which are already the latest Alpine 3.24 builds with no fixed
  version published, so they are **not** claimed as zero and cannot be remediated from the Dockerfiles.
- **Documentation (finding 7).** This section, the Npgsql-UTC and lowercase-PK bug entries above, and
  the corrected test counts (**26 backend / 17 frontend**) reflect what was actually run; the online PR
  description was updated from 16/6 to the final counts with the corrective commits listed.

**Second-round verification totals:** backend `dotnet build` 0 warnings, **26/26 backend tests** pass
(Testcontainers PostgreSQL); **17/17 frontend tests** pass; production frontend build clean; fresh
`docker compose up --build` all three tiers healthy with `/api/health/ready` 200 and 0 future-dated
seed rows; real `origin/master`→HEAD image upgrade preserved ids/counts and repaired the 12 future
rows; stale-migration readiness 503 / current 200; browser pass at 1440×900 with 0 axe violations and
a clean console; refreshed Docker Scout scan (remaining upstream CVEs reported honestly above);
`git diff --check` clean.
