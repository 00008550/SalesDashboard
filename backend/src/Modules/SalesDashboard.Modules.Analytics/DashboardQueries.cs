using Dapper;
using Npgsql;

namespace SalesDashboard.Modules.Analytics;

// Raw rows returned by the analytical SQL. Column names use snake_case; Dapper maps them with
// MatchNamesWithUnderscores (set once below).
internal sealed class TotalsRow { public decimal Revenue { get; set; } public decimal Cost { get; set; } public int PaidSales { get; set; } }
internal sealed class ManagerAggRow
{
    public Guid ManagerId { get; set; }
    public string FullName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string AvatarColor { get; set; } = "";
    public bool IsActive { get; set; }
    public int PaidSales { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}
internal sealed class TrendRow { public DateTimeOffset BucketStart { get; set; } public decimal Revenue { get; set; } public decimal GrossProfit { get; set; } public int PaidSales { get; set; } }
internal sealed class CategoryRow { public Guid CategoryId { get; set; } public string Name { get; set; } = ""; public decimal Revenue { get; set; } public decimal GrossProfit { get; set; } }
internal sealed class ProductAggRow { public Guid ProductId { get; set; } public string Name { get; set; } = ""; public string Category { get; set; } = ""; public decimal Revenue { get; set; } public decimal GrossProfit { get; set; } public int UnitsSold { get; set; } }
internal sealed class RecentRow
{
    public Guid SaleId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Manager { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Company { get; set; } = "";
    public string Products { get; set; } = "";
    public int ItemCount { get; set; }
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
}

/// <summary>
/// Analytical reads via Dapper over Npgsql. Every aggregate is computed at the <b>one-sale grain</b>:
/// items are rolled up to a per-sale fact first, sale status is applied, then sale-level facts are
/// aggregated — so SaleItem rows never inflate Paid Sales or Average Check. Trend buckets are
/// truncated in the reporting timezone (Etc/GMT-3 = UTC+3) and zero-filled with generate_series.
/// The SQL is coupled to the PostgreSQL schema (documented), not to EF entity types.
/// </summary>
public sealed class DashboardQueries(NpgsqlDataSource dataSource)
{
    static DashboardQueries() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    internal async Task<NpgsqlConnection> OpenAsync(CancellationToken ct) => await dataSource.OpenConnectionAsync(ct);

    private const string SaleFacts = """
        WITH sale_facts AS (
            SELECT s.id, s.status,
                   COALESCE(SUM(i.unit_price * i.quantity), 0) AS revenue,
                   COALESCE(SUM(i.unit_cost  * i.quantity), 0) AS cost
            FROM sales.sales s
            LEFT JOIN sales.sale_items i ON i.sale_id = s.id
            WHERE s.occurred_at >= @start AND s.occurred_at < @end
            GROUP BY s.id, s.status
        )
        """;

    internal async Task<(decimal Revenue, decimal Cost, int PaidSales)> GetTotalsAsync(
        NpgsqlConnection conn, Window w, CancellationToken ct)
    {
        const string sql = SaleFacts + """
            SELECT COALESCE(SUM(revenue) FILTER (WHERE status = 'Paid'), 0)::numeric AS revenue,
                   COALESCE(SUM(cost)    FILTER (WHERE status = 'Paid'), 0)::numeric AS cost,
                   (COUNT(*) FILTER (WHERE status = 'Paid'))::int AS paid_sales
            FROM sale_facts;
            """;
        var r = await conn.QuerySingleAsync<TotalsRow>(new CommandDefinition(sql, new { start = w.Start, end = w.End }, cancellationToken: ct));
        return (r.Revenue, r.Cost, r.PaidSales);
    }

    internal async Task<IReadOnlyList<ManagerAggRow>> GetManagerAggregatesAsync(
        NpgsqlConnection conn, Window w, CancellationToken ct)
    {
        const string sql = """
            WITH sale_facts AS (
                SELECT s.id, s.manager_id,
                       COALESCE(SUM(i.unit_price * i.quantity), 0) AS revenue,
                       COALESCE(SUM(i.unit_cost  * i.quantity), 0) AS cost
                FROM sales.sales s
                LEFT JOIN sales.sale_items i ON i.sale_id = s.id
                WHERE s.status = 'Paid' AND s.occurred_at >= @start AND s.occurred_at < @end
                GROUP BY s.id, s.manager_id
            )
            SELECT m.id AS manager_id, m.full_name, m.initials, m.avatar_color, m.is_active,
                   (COUNT(f.id))::int AS paid_sales,
                   COALESCE(SUM(f.revenue), 0)::numeric AS revenue,
                   COALESCE(SUM(f.cost), 0)::numeric AS cost
            FROM sale_facts f
            JOIN people.managers m ON m.id = f.manager_id
            GROUP BY m.id, m.full_name, m.initials, m.avatar_color, m.is_active;
            """;
        return (await conn.QueryAsync<ManagerAggRow>(new CommandDefinition(sql, new { start = w.Start, end = w.End }, cancellationToken: ct))).AsList();
    }

    internal async Task<IReadOnlyList<TrendRow>> GetTrendAsync(
        NpgsqlConnection conn, ResolvedPeriod period, CancellationToken ct)
    {
        const string sql = """
            WITH sale_facts AS (
                SELECT s.id, s.occurred_at,
                       COALESCE(SUM(i.unit_price * i.quantity), 0) AS revenue,
                       COALESCE(SUM(i.unit_cost  * i.quantity), 0) AS cost
                FROM sales.sales s
                LEFT JOIN sales.sale_items i ON i.sale_id = s.id
                WHERE s.status = 'Paid' AND s.occurred_at >= @start AND s.occurred_at < @end
                GROUP BY s.id, s.occurred_at
            ),
            facts AS (
                SELECT date_trunc(@unit, occurred_at AT TIME ZONE 'Etc/GMT-3') AS bucket_local,
                       SUM(revenue) AS revenue, SUM(cost) AS cost, (COUNT(*))::int AS paid_sales
                FROM sale_facts
                GROUP BY 1
            ),
            buckets AS (
                -- generate_series includes its stop value, so drop any bucket at/after the exclusive
                -- end to keep the window half-open [start, end).
                SELECT gs AS bucket_local
                FROM generate_series(
                        date_trunc(@unit, (@start AT TIME ZONE 'Etc/GMT-3')),
                        (@end AT TIME ZONE 'Etc/GMT-3'),
                        @step::interval) AS gs
                WHERE gs < (@end AT TIME ZONE 'Etc/GMT-3')
            )
            SELECT (b.bucket_local AT TIME ZONE 'Etc/GMT-3') AS bucket_start,
                   COALESCE(f.revenue, 0)::numeric AS revenue,
                   (COALESCE(f.revenue, 0) - COALESCE(f.cost, 0))::numeric AS gross_profit,
                   COALESCE(f.paid_sales, 0) AS paid_sales
            FROM buckets b
            LEFT JOIN facts f ON f.bucket_local = b.bucket_local
            ORDER BY b.bucket_local;
            """;
        var p = new { start = period.Current.Start, end = period.Current.End, unit = period.TruncUnit, step = period.StepInterval };
        return (await conn.QueryAsync<TrendRow>(new CommandDefinition(sql, p, cancellationToken: ct))).AsList();
    }

    internal async Task<IReadOnlyList<CategoryRow>> GetCategoriesAsync(
        NpgsqlConnection conn, Window w, CancellationToken ct)
    {
        const string sql = """
            SELECT c.id AS category_id, c.name,
                   COALESCE(SUM(i.unit_price * i.quantity), 0)::numeric AS revenue,
                   COALESCE(SUM((i.unit_price - i.unit_cost) * i.quantity), 0)::numeric AS gross_profit
            FROM sales.sales s
            JOIN sales.sale_items i ON i.sale_id = s.id
            JOIN catalog.products p ON p.id = i.product_id
            JOIN catalog.categories c ON c.id = p.category_id
            WHERE s.status = 'Paid' AND s.occurred_at >= @start AND s.occurred_at < @end
            GROUP BY c.id, c.name
            ORDER BY revenue DESC;
            """;
        return (await conn.QueryAsync<CategoryRow>(new CommandDefinition(sql, new { start = w.Start, end = w.End }, cancellationToken: ct))).AsList();
    }

    internal async Task<IReadOnlyList<ProductAggRow>> GetTopProductsAsync(
        NpgsqlConnection conn, Window w, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS product_id, p.name, c.name AS category,
                   COALESCE(SUM(i.unit_price * i.quantity), 0)::numeric AS revenue,
                   COALESCE(SUM((i.unit_price - i.unit_cost) * i.quantity), 0)::numeric AS gross_profit,
                   (COALESCE(SUM(i.quantity), 0))::int AS units_sold
            FROM sales.sales s
            JOIN sales.sale_items i ON i.sale_id = s.id
            JOIN catalog.products p ON p.id = i.product_id
            JOIN catalog.categories c ON c.id = p.category_id
            WHERE s.status = 'Paid' AND s.occurred_at >= @start AND s.occurred_at < @end
            GROUP BY p.id, p.name, c.name
            ORDER BY revenue DESC
            LIMIT @limit;
            """;
        return (await conn.QueryAsync<ProductAggRow>(new CommandDefinition(sql, new { start = w.Start, end = w.End, limit }, cancellationToken: ct))).AsList();
    }

    internal async Task<IReadOnlyList<RecentRow>> GetRecentSalesAsync(
        NpgsqlConnection conn, Window w, int limit, CancellationToken ct)
    {
        const string sql = """
            WITH sale_facts AS (
                SELECT s.id, s.manager_id, s.customer_id, s.occurred_at, s.status,
                       COALESCE(SUM(i.unit_price * i.quantity), 0) AS amount,
                       COALESCE(SUM(i.unit_cost  * i.quantity), 0) AS cost,
                       (COUNT(i.id))::int AS item_count
                FROM sales.sales s
                LEFT JOIN sales.sale_items i ON i.sale_id = s.id
                WHERE s.occurred_at >= @start AND s.occurred_at < @end
                GROUP BY s.id, s.manager_id, s.customer_id, s.occurred_at, s.status
            )
            SELECT f.id AS sale_id, f.occurred_at, m.full_name AS manager,
                   cu.name AS customer, cu.company,
                   COALESCE((SELECT string_agg(p.name, ', ')
                             FROM sales.sale_items i2 JOIN catalog.products p ON p.id = i2.product_id
                             WHERE i2.sale_id = f.id), '') AS products,
                   f.item_count, f.status,
                   f.amount::numeric AS amount, f.cost::numeric AS cost,
                   (f.amount - f.cost)::numeric AS gross_profit
            FROM sale_facts f
            JOIN people.managers m ON m.id = f.manager_id
            JOIN people.customers cu ON cu.id = f.customer_id
            ORDER BY f.occurred_at DESC
            LIMIT @limit;
            """;
        return (await conn.QueryAsync<RecentRow>(new CommandDefinition(sql, new { start = w.Start, end = w.End, limit }, cancellationToken: ct))).AsList();
    }
}
