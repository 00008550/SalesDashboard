---
name: analytics-verification
description: Independent verification procedure for the dashboard's analytical results. Load when reviewing or validating any analytics output. The principle is to cross-check the API's numbers against INDEPENDENT PostgreSQL ground truth via psql — never by re-running the same application query and calling that verification.
---

# Analytics verification — independent ground truth

**Rule:** the API's analytical result must be reconciled against an **independent** SQL query you
write by hand against the running Postgres, not against the application's own query path. Two
different derivations agreeing is evidence; one derivation agreeing with itself is not. Business
definitions come from [`sales-domain`](../sales-domain/SKILL.md) — this skill only checks them.

## Get a shell on the running database

```bash
docker compose exec -T db psql -U app -d salesdashboard -v ON_ERROR_STOP=1
```

Pick a concrete window from the API response's echoed `current`/`previous` `{start,end}` (UTC,
half-open) and reuse those exact instants in SQL — do not re-derive the window in SQL, or you test
two things at once.

## The one-sale grain guard (write it this way every time)

Aggregate items to one row per sale first, then apply status, then aggregate. Counting item rows is
the classic bug this catches.

```sql
WITH sale_facts AS (          -- one row per Sale
  SELECT s.id, s.manager_id, s.status,
         COALESCE(SUM(i.sale_price * i.quantity), 0) AS revenue,
         COALESCE(SUM(i.cost       * i.quantity), 0) AS cost
  FROM sales.sales s
  LEFT JOIN sales.sale_items i ON i.sale_id = s.id
  WHERE s.occurred_at >= :start AND s.occurred_at < :end   -- half-open, UTC
  GROUP BY s.id, s.manager_id, s.status
)
SELECT COUNT(*) FILTER (WHERE status = 'Paid')                      AS paid_sales,
       COALESCE(SUM(revenue) FILTER (WHERE status = 'Paid'), 0)     AS revenue,
       COALESCE(SUM(revenue - cost) FILTER (WHERE status = 'Paid'), 0) AS gross_profit
FROM sale_facts;
```

## Checklist (reconcile each against the API JSON)

- **Revenue / Cost / Gross Profit** — match `summary` within a cent.
- **Paid Sales** — equals the SQL count above, and is **not** inflated by SaleItem rows (verify with
  a sale that has ≥2 items: `paid_sales` must not rise with item count).
- **Average Check** — `revenue / paid_sales`, and **null** when `paid_sales = 0`.
- **Margin** — `gross_profit / revenue`, and **null** when `revenue = 0`.
- **Status treatment** — repeat with `status='Cancelled'` and `'Refunded'`: both contribute 0 and
  are excluded from the count, yet still exist as rows (operationally visible).
- **Current & previous boundaries** — a sale exactly at `start` is included; exactly at `end` is
  excluded; the previous window matches the per-preset semantic rule in `sales-domain`.
- **Manager ranking** — both server-ranked collections (`rankings.grossProfit`,
  `rankings.averageCheck`) have correct order, correct per-row `rank`, and the deterministic
  tie-break; ranking covers managers with ≥1 Paid sale regardless of `active`.
- **Trend & categories reconcile with `summary`** — these are **full partitions**, so
  Σ category revenue = summary revenue and Σ trend-bucket revenue = summary revenue.
- **Top products does NOT reconcile to the total** — it is a limited top-N list. Do **not** assert
  Σ top-product revenue = summary revenue. Instead verify each listed product's figures against SQL
  and that the ordering/limit is correct.

## Reporting
State the window used, the SQL you ran, its result, the API value, and PASS/FAIL per line. A mismatch
is a finding with both numbers quoted — never "looks right".
