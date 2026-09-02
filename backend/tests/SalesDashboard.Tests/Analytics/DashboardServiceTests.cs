using Microsoft.EntityFrameworkCore;
using Npgsql;
using SalesDashboard.Contracts;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Modules.Analytics;
using SalesDashboard.Modules.Catalog.Domain;
using SalesDashboard.Modules.People.Domain;
using SalesDashboard.Modules.Sales.Domain;
using SalesDashboard.Tests.Fixtures;
using SalesDashboard.Tests.Seeding;

namespace SalesDashboard.Tests.Analytics;

/// <summary>
/// Analytics correctness against a small hand-crafted dataset on real PostgreSQL. Covers the
/// one-sale grain, status treatment, exact date boundaries, previous-period comparison, ranking +
/// ties, reconciliation of trend/categories with the summary, and the zero-revenue period.
/// </summary>
[Collection("postgres")]
public sealed class DashboardServiceTests(PostgresFixture fx)
{
    private static DateTimeOffset Msk(int y, int mo, int d, int h = 12, int mi = 0) =>
        new(y, mo, d, h, mi, 0, TimeSpan.FromHours(3));

    private static readonly Guid M1 = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Alpha
    private static readonly Guid M2 = Guid.Parse("22222222-2222-2222-2222-222222222222"); // Beta
    private static readonly Guid M3 = Guid.Parse("33333333-3333-3333-3333-333333333333"); // no sales
    private static readonly Guid C1 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Cat1 = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid Cat2 = Guid.Parse("a2222222-2222-2222-2222-222222222222");
    private static readonly Guid P1 = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid P2 = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    private static Manager Mgr(Guid id, string name) => new()
    { Id = id, FullName = name, Title = "AE", Team = "T", IsActive = true, Initials = name[..2], AvatarColor = "#000000" };

    private static Sale Sale(Guid mgr, DateTimeOffset at, SaleStatus status, params (Guid product, int qty, decimal price, decimal cost)[] items) => new()
    {
        Id = Guid.NewGuid(),
        ManagerId = mgr,
        CustomerId = C1,
        OccurredAt = at.ToUniversalTime(), // Npgsql timestamptz requires UTC (offset 0)
        Status = status,
        Items = [.. items.Select(i => new SaleItem { Id = Guid.NewGuid(), ProductId = i.product, Quantity = i.qty, UnitPrice = i.price, UnitCost = i.cost })],
    };

    private async Task<string> SeedMainAsync()
    {
        var conn = await fx.NewEmptyDatabaseConnectionStringAsync();
        await using var db = new WriteDbContext(PostgresFixture.OptionsFor(conn));
        await db.Database.MigrateAsync();

        db.Managers.AddRange(Mgr(M1, "Alpha Ivanov"), Mgr(M2, "Beta Petrov"), Mgr(M3, "Gamma Sokolov"));
        db.Customers.Add(new Customer { Id = C1, Name = "Contact", Company = "Acme", Segment = CustomerSegment.Smb });
        db.Categories.AddRange(new Category { Id = Cat1, Name = "Drones" }, new Category { Id = Cat2, Name = "Accessories" });
        db.Products.AddRange(
            new Product { Id = P1, Name = "Drone One", Sku = "P1", CategoryId = Cat1, BasePrice = 100, BaseCost = 60 },
            new Product { Id = P2, Name = "Strap", Sku = "P2", CategoryId = Cat2, BasePrice = 50, BaseCost = 20 });

        db.Sales.AddRange(
            // Current window (June): Paid A (multi-item), B, and E exactly on the start boundary.
            Sale(M1, Msk(2026, 6, 10), SaleStatus.Paid, (P1, 2, 100, 60), (P2, 1, 50, 20)),   // rev 250, cost 140
            Sale(M2, Msk(2026, 6, 15), SaleStatus.Paid, (P1, 1, 200, 100)),                    // rev 200, cost 100
            Sale(M1, Msk(2026, 6, 1, 0, 0), SaleStatus.Paid, (P1, 1, 100, 40)),                // start boundary -> included
                                                                                               // Excluded from financials:
            Sale(M1, Msk(2026, 6, 12), SaleStatus.Cancelled, (P1, 1, 999, 1)),
            Sale(M2, Msk(2026, 6, 18), SaleStatus.Refunded, (P2, 1, 999, 1)),
            Sale(M2, Msk(2026, 7, 1, 0, 0), SaleStatus.Paid, (P1, 1, 500, 100)),               // end boundary -> excluded
                                                                                               // Previous window (May): one Paid sale by M1.
            Sale(M1, Msk(2026, 5, 15), SaleStatus.Paid, (P1, 1, 100, 50)));                    // rev 100, cost 50

        await db.SaveChangesAsync();
        return conn;
    }

    private static DashboardService Service(string conn, NpgsqlDataSource ds) =>
        new(new DashboardQueries(ds), new FixedClock(Msk(2026, 6, 20)));

    [Fact]
    public async Task Computes_financials_grain_status_boundaries_and_previous()
    {
        var conn = await SeedMainAsync();
        await using var ds = NpgsqlDataSource.Create(conn);
        var r = await Service(conn, ds).BuildAsync(null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), default);

        // One-sale grain: A has two items but counts as one Paid sale -> 3 paid sales (A, B, E), not 4.
        Assert.Equal(3, r.Summary.PaidSales.Current);
        Assert.Equal(550m, r.Summary.Revenue.Current);        // 250 + 200 + 100 (Cancelled/Refunded excluded)
        Assert.Equal(270m, r.Summary.GrossProfit.Current);    // 550 - 280
        Assert.Equal(550m / 3, r.Summary.AverageCheck.Current);
        Assert.Equal(280m, r.Summary.Cost.Current);       // 140 + 100 + 40
        Assert.Equal(50m, r.Summary.Cost.Previous);       // May sale cost
        Assert.Equal(270.0 / 550.0, r.Summary.Margin.Current!.Value, 6);
        // Margin delta is in percentage points already (×100): (0.4909… − 0.5) × 100.
        Assert.Equal((270.0 / 550.0 - 0.5) * 100, r.Summary.Margin.DeltaPp!.Value, 4);

        // Previous period (May): revenue 100 -> +450% change.
        Assert.Equal(100m, r.Summary.Revenue.Previous);
        Assert.Equal(4.5, r.Summary.Revenue.ChangePercent!.Value, 6);

        // Best manager + gross-profit ranking (M1 170 > M2 100); M3 has no sales and is omitted.
        Assert.Equal("Alpha Ivanov", r.Summary.BestManager!.Name);
        Assert.Equal(170m, r.Summary.BestManager.GrossProfit);
        Assert.Equal(2, r.Rankings.GrossProfit.Count);
        Assert.Equal("Alpha Ivanov", r.Rankings.GrossProfit[0].Name);
        Assert.Equal(1, r.Rankings.GrossProfit[0].Rank);
        Assert.Equal(2.4, r.Rankings.GrossProfit[0].GrossProfitChangePercent!.Value, 6); // 170 vs prev 50
        Assert.Null(r.Rankings.GrossProfit[1].GrossProfitChangePercent);                  // M2 had no previous

        // Average-check ranking orders differently: M2 (200) ahead of M1 (175).
        Assert.Equal("Beta Petrov", r.Rankings.AverageCheck[0].Name);

        // Trend and categories reconcile with the summary; top-N products need not.
        Assert.Equal(550m, r.Trend.Sum(t => t.Revenue));
        Assert.Equal(550m, r.Categories.Sum(c => c.Revenue));
        Assert.Equal(500m, r.TopProducts[0].Revenue); // P1 is the top product

        // Recent sales expose Cancelled/Refunded with ORIGINAL amounts (they don't reconcile to KPIs).
        Assert.Equal(5, r.RecentSales.Count); // A, B, C, D, E (F is out of window)
        Assert.Contains(r.RecentSales, s => s.Status == "Cancelled");
        var refunded = Assert.Single(r.RecentSales, s => s.Status == "Refunded");
        Assert.Equal(999m, refunded.Amount);
    }

    [Fact]
    public async Task Empty_period_yields_zeroed_summary_and_empty_blocks()
    {
        var conn = await SeedMainAsync();
        await using var ds = NpgsqlDataSource.Create(conn);
        var r = await Service(conn, ds).BuildAsync(null, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31), default);

        Assert.Equal(0m, r.Summary.Revenue.Current);
        Assert.Equal(0, r.Summary.PaidSales.Current);
        Assert.Null(r.Summary.Margin.Current);       // no NaN/Infinity
        Assert.Null(r.Summary.AverageCheck.Current);
        Assert.Null(r.Summary.BestManager);
        Assert.Empty(r.Rankings.GrossProfit);
        Assert.Empty(r.Categories);
        Assert.Empty(r.TopProducts);
        Assert.Empty(r.RecentSales);
        Assert.All(r.Trend, t => Assert.Equal(0, t.PaidSales)); // zero-filled, not missing
    }

    [Fact]
    public async Task Ranking_ties_break_deterministically_by_name()
    {
        var conn = await fx.NewEmptyDatabaseConnectionStringAsync();
        await using (var db = new WriteDbContext(PostgresFixture.OptionsFor(conn)))
        {
            await db.Database.MigrateAsync();
            var bravo = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
            var alpha = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            db.Managers.AddRange(Mgr(bravo, "Bravo"), Mgr(alpha, "Alpha"));
            db.Customers.Add(new Customer { Id = C1, Name = "X", Company = "Y", Segment = CustomerSegment.Smb });
            db.Categories.Add(new Category { Id = Cat1, Name = "Drones" });
            db.Products.Add(new Product { Id = P1, Name = "Drone", Sku = "P1", CategoryId = Cat1, BasePrice = 100, BaseCost = 50 });
            // Identical gross profit for both managers -> tie broken by name ascending.
            db.Sales.AddRange(
                Sale(bravo, Msk(2026, 6, 10), SaleStatus.Paid, (P1, 1, 100, 50)),
                Sale(alpha, Msk(2026, 6, 11), SaleStatus.Paid, (P1, 1, 100, 50)));
            await db.SaveChangesAsync();
        }

        await using var ds = NpgsqlDataSource.Create(conn);
        var r = await Service(conn, ds).BuildAsync(null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), default);

        Assert.Equal("Alpha", r.Rankings.GrossProfit[0].Name); // ties -> name ascending
        Assert.Equal("Bravo", r.Rankings.GrossProfit[1].Name);
    }
}
