using Npgsql;
using SalesDashboard.Modules.Analytics;
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
}
