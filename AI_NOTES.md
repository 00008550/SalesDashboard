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
- _(to be filled in during implementation)_
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
  `GET /api/dashboard` snapshot (period resolved once; one consistent DB snapshot).

## Where AI accelerated the work
- _(to be filled in)_

## Where AI got it wrong / what I changed or rejected
- _(to be filled in — recorded only when a genuine mistake occurs, not invented)_

## How generated code was verified
- _(to be filled in: build, tests, `docker compose up --build` from a clean state, manual dashboard check)_
