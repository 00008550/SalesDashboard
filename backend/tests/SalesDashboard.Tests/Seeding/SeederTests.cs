using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesDashboard.Contracts;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;
using SalesDashboard.Tests.Fixtures;

namespace SalesDashboard.Tests.Seeding;

/// <summary>A clock that always returns a fixed instant, so seed determinism is testable independent
/// of wall-clock time.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

[Collection("postgres")]
public sealed class SeederTests(PostgresFixture fx)
{
    private static DeterministicSeeder Seeder(WriteDbContext db, TimeProvider? clock = null) =>
        new(db, clock ?? TimeProvider.System, NullLogger<DeterministicSeeder>.Instance);

    [Fact]
    public async Task Seed_populates_the_required_scale_and_edge_cases()
    {
        var opts = await fx.NewMigratedDatabaseAsync();
        await using (var db = new WriteDbContext(opts))
            await Seeder(db).SeedAsync();

        await using var read = new WriteDbContext(opts);
        Assert.InRange(await read.Managers.CountAsync(), 15, 25);
        Assert.InRange(await read.Customers.CountAsync(), 50, 100);
        Assert.True(await read.Categories.CountAsync() >= 4);
        Assert.True(await read.Products.CountAsync() >= 30, "several dozen products");
        Assert.InRange(await read.Sales.CountAsync(), 2000, 5000);

        var statuses = await read.Sales.Select(s => s.Status).Distinct().ToListAsync();
        Assert.Contains(SaleStatus.Paid, statuses);
        Assert.Contains(SaleStatus.Cancelled, statuses);
        Assert.Contains(SaleStatus.Refunded, statuses);

        Assert.True(await read.Managers.AnyAsync(m => !m.IsActive), "an inactive manager must exist");
        Assert.True(
            await read.Sales.AnyAsync(s => s.Status == SaleStatus.Paid && s.Items.Count > 1),
            "a multi-item Paid sale must exist (the one-sale-grain guarantee depends on it)");

        var marker = await read.SeedState.SingleAsync();
        Assert.Equal(DeterministicSeeder.SeedVersion, marker.Version);
    }

    [Fact]
    public async Task Seed_is_idempotent_when_run_repeatedly()
    {
        var opts = await fx.NewMigratedDatabaseAsync();
        await using (var db1 = new WriteDbContext(opts))
            await Seeder(db1).SeedAsync();

        int salesAfterFirst, itemsAfterFirst;
        await using (var r1 = new WriteDbContext(opts))
        {
            salesAfterFirst = await r1.Sales.CountAsync();
            itemsAfterFirst = await r1.SaleItems.CountAsync();
        }

        await using (var db2 = new WriteDbContext(opts))
            await Seeder(db2).SeedAsync(); // second run must be a no-op

        await using var r2 = new WriteDbContext(opts);
        Assert.Equal(salesAfterFirst, await r2.Sales.CountAsync());
        Assert.Equal(itemsAfterFirst, await r2.SaleItems.CountAsync());
        Assert.Equal(1, await r2.SeedState.CountAsync());
    }

    [Fact]
    public async Task Seed_shape_is_deterministic_across_fresh_databases()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

        var a = await fx.NewMigratedDatabaseAsync();
        await using (var db = new WriteDbContext(a)) await Seeder(db, clock).SeedAsync();
        var b = await fx.NewMigratedDatabaseAsync();
        await using (var db = new WriteDbContext(b)) await Seeder(db, clock).SeedAsync();

        await using var ra = new WriteDbContext(a);
        await using var rb = new WriteDbContext(b);
        Assert.Equal(await ra.Sales.CountAsync(), await rb.Sales.CountAsync());
        Assert.Equal(await ra.SaleItems.CountAsync(), await rb.SaleItems.CountAsync());
        Assert.Equal(
            await ra.Managers.OrderBy(m => m.Id).Select(m => m.FullName).ToListAsync(),
            await rb.Managers.OrderBy(m => m.Id).Select(m => m.FullName).ToListAsync());
    }

    [Fact]
    public async Task Seed_never_produces_records_after_the_anchor()
    {
        var anchor = new DateTimeOffset(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);
        var opts = await fx.NewMigratedDatabaseAsync();
        await using (var db = new WriteDbContext(opts))
            await Seeder(db, new FixedClock(anchor)).SeedAsync();

        await using var read = new WriteDbContext(opts);
        var max = await read.Sales.MaxAsync(s => s.OccurredAt);
        Assert.True(max <= anchor, $"max occurred_at {max:o} must be <= anchor {anchor:o}");
    }

    /// <summary>
    /// Regression for the upgrade path: a database seeded by the pre-fix generator carries the same
    /// seed version (v1) but has sales after the captured anchor. Re-running the seeder must repair
    /// those rows non-destructively — clamping max(occurred_at) &lt;= seed_state.applied_at while
    /// preserving every sale/item id and the total row counts — rather than clearing and regenerating.
    /// </summary>
    [Fact]
    public async Task Upgrade_repairs_future_dated_sales_preserving_ids_and_counts()
    {
        var anchor = new DateTimeOffset(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);
        var opts = await fx.NewMigratedDatabaseAsync();
        await using (var db = new WriteDbContext(opts))
            await Seeder(db, new FixedClock(anchor)).SeedAsync();

        // Capture the pre-upgrade truth.
        int salesBefore, itemsBefore;
        List<Guid> allSaleIds, allItemIds;
        await using (var read = new WriteDbContext(opts))
        {
            salesBefore = await read.Sales.CountAsync();
            itemsBefore = await read.SaleItems.CountAsync();
            allSaleIds = await read.Sales.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
            allItemIds = await read.SaleItems.OrderBy(i => i.Id).Select(i => i.Id).ToListAsync();
        }

        // Simulate the pre-fix defect: push a batch of sales past the anchor, exactly as the old
        // generator (anchor + hours on the anchor day) would have. IDs and counts are untouched.
        var pushedIds = allSaleIds.Take(12).ToList();
        await using (var mutate = new WriteDbContext(opts))
        {
            var future = anchor.AddHours(13).UtcDateTime; // 13h after the anchor instant
            var affected = await mutate.Sales
                .Where(s => pushedIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OccurredAt, future));
            Assert.Equal(12, affected);
        }

        // Re-run the seeder against the same v1 database: the upgrade repair path runs.
        await using (var upgrade = new WriteDbContext(opts))
            await Seeder(upgrade, new FixedClock(anchor)).SeedAsync();

        await using var after = new WriteDbContext(opts);
        var appliedAt = (await after.SeedState.SingleAsync()).AppliedAt;

        var max = await after.Sales.MaxAsync(s => s.OccurredAt);
        Assert.True(max <= appliedAt, $"after upgrade, max occurred_at {max:o} must be <= applied_at {appliedAt:o}");

        // Non-destructive: counts and the exact id sets are preserved (no clear-and-regenerate).
        Assert.Equal(salesBefore, await after.Sales.CountAsync());
        Assert.Equal(itemsBefore, await after.SaleItems.CountAsync());
        Assert.Equal(allSaleIds, await after.Sales.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync());
        Assert.Equal(allItemIds, await after.SaleItems.OrderBy(i => i.Id).Select(i => i.Id).ToListAsync());

        // The repaired rows are the same 12 ids, now within the anchor's reporting day.
        foreach (var id in pushedIds)
            Assert.True(await after.Sales.AnyAsync(s => s.Id == id), $"repaired sale {id} must still exist");
    }
}
