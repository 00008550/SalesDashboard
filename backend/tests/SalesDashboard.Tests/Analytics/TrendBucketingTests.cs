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
/// Trend bucketing is half-open: buckets are zero-filled across [current.start, current.end) and no
/// bucket may start at or after current.end (PostgreSQL generate_series includes its stop value).
/// An empty migrated database is enough — the trend is zero-filled regardless of data.
/// </summary>
[Collection("postgres")]
public sealed class TrendBucketingTests(PostgresFixture fx)
{
    private static DateTimeOffset MskUtc(int y, int mo, int d) =>
        new DateTimeOffset(y, mo, d, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime();

    private async Task<DashboardService> ServiceAsync(DateTimeOffset now)
    {
        var conn = await fx.NewEmptyDatabaseConnectionStringAsync();
        await using (var db = new SalesDashboard.Infrastructure.Persistence.WriteDbContext(PostgresFixture.OptionsFor(conn)))
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(db.Database);
        return new DashboardService(new DashboardQueries(NpgsqlDataSource.Create(conn)), new FixedClock(now));
    }

    private async Task<(DashboardService Service, DbContextOptions<WriteDbContext> Options)>
        ServiceWithOptionsAsync(DateTimeOffset now)
    {
        var conn = await fx.NewEmptyDatabaseConnectionStringAsync();
        var options = PostgresFixture.OptionsFor(conn);
        await using (var db = new WriteDbContext(options))
            await db.Database.MigrateAsync();
        return (new DashboardService(new DashboardQueries(NpgsqlDataSource.Create(conn)), new FixedClock(now)), options);
    }

    [Fact]
    public async Task One_day_custom_range_yields_24_hourly_half_open_buckets()
    {
        // A one-day window is <= 2 days, so the granularity is hourly: 24 buckets, none at/after end.
        var svc = await ServiceAsync(default);
        var r = await svc.BuildAsync(null, new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 10), default);

        Assert.Equal("hour", r.Period.Granularity);
        Assert.Equal(24, r.Trend.Count);
        Assert.Equal(MskUtc(2026, 6, 10), r.Trend[0].BucketStart); // first bucket = MSK midnight of the day
        Assert.All(r.Trend, t => Assert.True(t.BucketStart < r.Period.Current.End));
    }

    [Fact]
    public async Task Thirty_day_custom_range_yields_thirty_daily_buckets()
    {
        var svc = await ServiceAsync(default);
        var r = await svc.BuildAsync(null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), default);

        Assert.Equal(30, r.Trend.Count);
        Assert.Equal(MskUtc(2026, 6, 1), r.Trend[0].BucketStart);
        Assert.Equal(MskUtc(2026, 6, 30), r.Trend[^1].BucketStart);
        Assert.True(r.Trend[^1].BucketStart < r.Period.Current.End);
    }

    [Fact]
    public async Task ThirtyOne_day_custom_range_yields_thirty_one_daily_buckets()
    {
        var svc = await ServiceAsync(default);
        var r = await svc.BuildAsync(null, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), default);

        Assert.Equal(31, r.Trend.Count);
        Assert.Equal(MskUtc(2026, 5, 1), r.Trend[0].BucketStart);
        Assert.True(r.Trend[^1].BucketStart < r.Period.Current.End);
    }

    [Fact]
    public async Task Previous_month_preset_covers_the_full_month_and_stays_before_end()
    {
        // now = 2026-07-15 MSK -> previous month is June (30 days).
        var svc = await ServiceAsync(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(3)));
        var r = await svc.BuildAsync("prevMonth", null, null, default);

        Assert.Equal(30, r.Trend.Count);
        Assert.Equal(MskUtc(2026, 6, 1), r.Trend[0].BucketStart);
        Assert.Equal(MskUtc(2026, 6, 30), r.Trend[^1].BucketStart);
        Assert.True(r.Trend[^1].BucketStart < r.Period.Current.End); // strictly before current.end (Jul 1)
    }

    [Fact]
    public async Task Weekly_partial_first_bucket_is_labelled_at_period_start_and_keeps_its_aggregation()
    {
        // 94 inclusive days selects weekly granularity and starts on a Wednesday. PostgreSQL groups
        // the first two sales into the Mon Jan 5 calendar week, but the public label is clipped to the
        // requested Wed Jan 7 start. A third sale belongs to the next calendar week.
        var (svc, options) = await ServiceWithOptionsAsync(default);
        var managerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await using (var db = new WriteDbContext(options))
        {
            db.Managers.Add(new Manager
            {
                Id = managerId,
                FullName = "Weekly Manager",
                Title = "Account Executive",
                Team = "Test",
                Initials = "WM",
                AvatarColor = "#1D4ED8",
            });
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Name = "Weekly Customer",
                Company = "Bucket Co",
                Segment = CustomerSegment.Smb,
            });
            db.Categories.Add(new Category { Id = categoryId, Name = "Weekly Category" });
            db.Products.Add(new Product
            {
                Id = productId,
                CategoryId = categoryId,
                Name = "Weekly Product",
                Sku = "WEEK-1",
                BasePrice = 100m,
                BaseCost = 40m,
            });

            AddPaidSale(db, managerId, customerId, productId, MskUtc(2026, 1, 7).AddHours(9), 100m, 40m);
            AddPaidSale(db, managerId, customerId, productId, MskUtc(2026, 1, 11).AddHours(12), 200m, 80m);
            AddPaidSale(db, managerId, customerId, productId, MskUtc(2026, 1, 12).AddHours(8), 300m, 120m);
            await db.SaveChangesAsync();
        }

        var result = await svc.BuildAsync(
            null,
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 4, 10),
            default);

        Assert.Equal("week", result.Period.Granularity);
        Assert.Equal(result.Period.Current.Start, result.Trend[0].BucketStart);
        Assert.Equal(300m, result.Trend[0].Revenue);
        Assert.Equal(180m, result.Trend[0].GrossProfit);
        Assert.Equal(2, result.Trend[0].PaidSales);
        Assert.Equal(MskUtc(2026, 1, 12), result.Trend[1].BucketStart);
        Assert.Equal(300m, result.Trend[1].Revenue);
        Assert.Equal(result.Summary.Revenue.Current, result.Trend.Sum(t => t.Revenue));
        Assert.Equal(result.Summary.GrossProfit.Current, result.Trend.Sum(t => t.GrossProfit));
        Assert.Equal(result.Summary.PaidSales.Current, result.Trend.Sum(t => t.PaidSales));
        Assert.All(result.Trend, t => Assert.InRange(
            t.BucketStart,
            result.Period.Current.Start,
            result.Period.Current.End.AddTicks(-1)));
    }

    private static void AddPaidSale(
        WriteDbContext db,
        Guid managerId,
        Guid customerId,
        Guid productId,
        DateTimeOffset occurredAt,
        decimal price,
        decimal cost)
    {
        db.Sales.Add(new Sale
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            CustomerId = customerId,
            OccurredAt = occurredAt,
            Status = SaleStatus.Paid,
            Items =
            [
                new SaleItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = price,
                    UnitCost = cost,
                },
            ],
        });
    }
}
