using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SalesDashboard.Contracts;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;
using SalesDashboard.Modules.Sales.Domain;
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

    [Fact]
    public async Task InitialSchema_v1_upgrade_repairs_only_positive_matches_and_preserves_ids_and_counts()
    {
        var legacy = await CreateRepresentativeLegacyV1DatabaseAsync();
        await MigrateToLatestAsync(legacy.Options);
        var salesBefore = await SalesSnapshotAsync(legacy.Options);
        var itemsBefore = await ItemsSnapshotAsync(legacy.Options);
        var legitimateBefore = Assert.Single(salesBefore, s => s.Id == legacy.LegitimateSaleId);
        Assert.Equal(legacy.OriginalSaleIds, salesBefore.Select(s => s.Id));
        Assert.Equal(legacy.OriginalItemIds, itemsBefore.Select(i => i.Id));

        await using (var migrated = new WriteDbContext(legacy.Options))
        {
            var pending = await migrated.SeedState.SingleAsync();
            Assert.Equal(0, pending.RepairVersion);
        }

        await using (var upgrade = new WriteDbContext(legacy.Options))
            await Seeder(upgrade, new FixedClock(legacy.Anchor)).SeedAsync();

        var salesAfter = await SalesSnapshotAsync(legacy.Options);
        var itemsAfter = await ItemsSnapshotAsync(legacy.Options);

        Assert.Equal(salesBefore.Count, salesAfter.Count);
        Assert.Equal(itemsBefore.Count, itemsAfter.Count);
        Assert.Equal(salesBefore.Select(s => s.Id), salesAfter.Select(s => s.Id));
        Assert.Equal(itemsBefore.Select(i => i.Id), itemsAfter.Select(i => i.Id));
        Assert.Equal(itemsBefore, itemsAfter);

        foreach (var candidate in legacy.Candidates)
        {
            var repaired = Assert.Single(salesAfter, s => s.Id == candidate.SaleId);
            Assert.NotEqual(candidate.LegacyOccurredAt, repaired.OccurredAt);
            Assert.True(repaired.OccurredAt <= legacy.Anchor);
        }

        // The arbitrary Paid sale at applied_at + 2 days is deliberately present before the repair.
        // It does not have the deterministic legacy id+timestamp signature and remains byte-for-byte
        // unchanged even though its timestamp is later than applied_at.
        Assert.Equal(legitimateBefore, Assert.Single(salesAfter, s => s.Id == legacy.LegitimateSaleId));

        await using var after = new WriteDbContext(legacy.Options);
        var marker = await after.SeedState.SingleAsync();
        Assert.Equal(DeterministicSeeder.LatestRepairVersion, marker.RepairVersion);
        Assert.Equal(0, await after.Sales.CountAsync(
            s => legacy.CandidateIds.Contains(s.Id) && s.OccurredAt > legacy.Anchor));
    }

    [Fact]
    public async Task Legacy_v1_repair_is_idempotent_across_application_restarts()
    {
        var legacy = await CreateRepresentativeLegacyV1DatabaseAsync();
        await MigrateToLatestAsync(legacy.Options);
        await using (var first = new WriteDbContext(legacy.Options))
            await Seeder(first, new FixedClock(legacy.Anchor)).SeedAsync();

        var salesAfterFirst = await SalesSnapshotAsync(legacy.Options);
        var itemsAfterFirst = await ItemsSnapshotAsync(legacy.Options);

        for (var restart = 0; restart < 2; restart++)
        {
            await MigrateToLatestAsync(legacy.Options);
            await using var db = new WriteDbContext(legacy.Options);
            await Seeder(db, new FixedClock(legacy.Anchor.AddDays(30 + restart))).SeedAsync();
        }

        Assert.Equal(salesAfterFirst, await SalesSnapshotAsync(legacy.Options));
        Assert.Equal(itemsAfterFirst, await ItemsSnapshotAsync(legacy.Options));
    }

    [Fact]
    public async Task Legitimate_post_seed_sale_is_byte_for_byte_unchanged_across_restarts()
    {
        var anchor = new DateTimeOffset(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);
        var options = await fx.NewMigratedDatabaseAsync();
        await using (var seed = new WriteDbContext(options))
            await Seeder(seed, new FixedClock(anchor)).SeedAsync();

        Guid saleId;
        await using (var db = new WriteDbContext(options))
        {
            saleId = Guid.NewGuid();
            var managerId = await db.Managers.Select(m => m.Id).FirstAsync();
            var customerId = await db.Customers.Select(c => c.Id).FirstAsync();
            var productId = await db.Products.Select(p => p.Id).FirstAsync();
            db.Sales.Add(new Sale
            {
                Id = saleId,
                ManagerId = managerId,
                CustomerId = customerId,
                OccurredAt = anchor.AddDays(2),
                Status = SaleStatus.Paid,
                Items =
                [
                    new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        Quantity = 3,
                        UnitPrice = 1234.56m,
                        UnitCost = 789.01m,
                    },
                ],
            });
            await db.SaveChangesAsync();
        }

        var saleBefore = Assert.Single(await SalesSnapshotAsync(options), s => s.Id == saleId);
        var itemBefore = Assert.Single(await ItemsForSaleSnapshotAsync(options, saleId));

        for (var restart = 0; restart < 2; restart++)
        {
            await MigrateToLatestAsync(options);
            await using var db = new WriteDbContext(options);
            await Seeder(db, new FixedClock(anchor.AddDays(10 + restart))).SeedAsync();
        }

        Assert.Equal(saleBefore, Assert.Single(await SalesSnapshotAsync(options), s => s.Id == saleId));
        Assert.Equal(itemBefore, Assert.Single(await ItemsForSaleSnapshotAsync(options, saleId)));
    }

    private const string InitialSchemaMigration = "20260902041021_InitialSchema";
    private static readonly DateTimeOffset LegacyAnchor =
        new(2026, 9, 2, 9, 30, 0, TimeSpan.Zero);

    // Golden signatures captured from the committed origin/master v1 generator at LegacyAnchor.
    // This fixture intentionally does not call the production replay logic: if that logic drifts,
    // these rows stop matching and the upgrade regression fails instead of manufacturing its own
    // matching input.
    private static readonly LegacyV1Signature[] LegacyV1GoldenCandidates =
    [
        new(new Guid("8248583c-2a41-9cd4-9d6f-98acc5d99b80"), new DateTimeOffset(2026, 9, 2, 11, 56, 3, TimeSpan.Zero)),
        new(new Guid("dfcb6e14-dd12-a7cc-747b-5174c14d248f"), new DateTimeOffset(2026, 9, 2, 14, 45, 47, TimeSpan.Zero)),
        new(new Guid("f9c0c42a-f2ef-60f6-83a3-8004c676fce7"), new DateTimeOffset(2026, 9, 3, 7, 0, 23, TimeSpan.Zero)),
        new(new Guid("89f30c5d-a065-e8ea-35fd-8ab533ecb0b3"), new DateTimeOffset(2026, 9, 3, 7, 31, 10, TimeSpan.Zero)),
        new(new Guid("6e366b89-dea8-414a-1e22-d72cf27e9d06"), new DateTimeOffset(2026, 9, 2, 22, 56, 10, TimeSpan.Zero)),
        new(new Guid("ec2da43b-d3bc-cab1-7549-bfc24d631249"), new DateTimeOffset(2026, 9, 3, 8, 54, 36, TimeSpan.Zero)),
        new(new Guid("c48ff4e0-718b-c2d0-3651-d76ad9905d12"), new DateTimeOffset(2026, 9, 3, 5, 0, 15, TimeSpan.Zero)),
        new(new Guid("7ca448b1-10c3-6aba-42bc-242e5081f1f5"), new DateTimeOffset(2026, 9, 2, 17, 13, 57, TimeSpan.Zero)),
        new(new Guid("a1a75bb7-3d3e-fd5d-13db-69b089c5ff68"), new DateTimeOffset(2026, 9, 2, 19, 9, 50, TimeSpan.Zero)),
        new(new Guid("4f097a35-853c-9fa4-1c59-9ef9cb87738b"), new DateTimeOffset(2026, 9, 2, 17, 5, 24, TimeSpan.Zero)),
        new(new Guid("2fe8ba5a-d01d-ffea-c7fe-9a16a41ab6c4"), new DateTimeOffset(2026, 9, 3, 0, 36, 22, TimeSpan.Zero)),
        new(new Guid("39561048-b975-37b8-137f-9f0e208c712d"), new DateTimeOffset(2026, 9, 2, 15, 44, 3, TimeSpan.Zero)),
    ];

    private sealed record LegacyV1Signature(Guid SaleId, DateTimeOffset LegacyOccurredAt);

    private sealed record LegacyDatabase(
        DbContextOptions<WriteDbContext> Options,
        DateTimeOffset Anchor,
        IReadOnlyList<LegacyV1Signature> Candidates,
        Guid[] CandidateIds,
        Guid LegitimateSaleId,
        Guid[] OriginalSaleIds,
        Guid[] OriginalItemIds);

    private sealed record SaleSnapshot(
        Guid Id,
        Guid ManagerId,
        Guid CustomerId,
        DateTimeOffset OccurredAt,
        SaleStatus Status);

    private sealed record ItemSnapshot(
        Guid Id,
        Guid SaleId,
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal UnitCost);

    /// <summary>
    /// Builds an InitialSchema-era database containing the exact id+timestamp signatures emitted by
    /// the original deterministic v1 generator, plus one ordinary later Paid sale. Minimal parent and
    /// item rows keep the fixture focused while exercising the real origin migration lineage.
    /// </summary>
    private async Task<LegacyDatabase> CreateRepresentativeLegacyV1DatabaseAsync()
    {
        var connectionString = await fx.NewEmptyDatabaseConnectionStringAsync();
        var options = PostgresFixture.OptionsFor(connectionString);

        await using (var db = new WriteDbContext(options))
            await db.Database.GetService<IMigrator>().MigrateAsync(InitialSchemaMigration);
        var candidates = LegacyV1GoldenCandidates;
        Assert.Equal(12, candidates.Length);

        var managerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var legitimateSaleId = Guid.NewGuid();
        var itemIds = new List<Guid>(candidates.Length + 1);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using (var parents = conn.CreateCommand())
        {
            parents.CommandText = """
                INSERT INTO people.managers
                    ("Id", full_name, title, team, is_active, initials, avatar_color)
                VALUES (@manager_id, 'Legacy Manager', 'Account Executive', 'Legacy', TRUE, 'LM', '#1D4ED8');
                INSERT INTO people.customers ("Id", name, company, segment)
                VALUES (@customer_id, 'Legacy Customer', 'Legacy Co', 'Smb');
                INSERT INTO catalog.categories ("Id", name)
                VALUES (@category_id, 'Legacy Category');
                INSERT INTO catalog.products ("Id", name, sku, category_id, base_price, base_cost)
                VALUES (@product_id, 'Legacy Product', 'LEGACY-1', @category_id, 100, 40);
                INSERT INTO ops.seed_state (id, version, applied_at)
                VALUES (1, @seed_version, @anchor);
                """;
            parents.Parameters.AddWithValue("manager_id", managerId);
            parents.Parameters.AddWithValue("customer_id", customerId);
            parents.Parameters.AddWithValue("category_id", categoryId);
            parents.Parameters.AddWithValue("product_id", productId);
            parents.Parameters.AddWithValue("seed_version", DeterministicSeeder.SeedVersion);
            parents.Parameters.AddWithValue("anchor", LegacyAnchor);
            await parents.ExecuteNonQueryAsync();
        }

        foreach (var candidate in candidates)
        {
            var itemId = Guid.NewGuid();
            itemIds.Add(itemId);
            await InsertLegacySaleAsync(
                conn,
                candidate.SaleId,
                itemId,
                managerId,
                customerId,
                productId,
                candidate.LegacyOccurredAt,
                100m,
                40m);
        }

        var legitimateItemId = Guid.NewGuid();
        itemIds.Add(legitimateItemId);
        await InsertLegacySaleAsync(
            conn,
            legitimateSaleId,
            legitimateItemId,
            managerId,
            customerId,
            productId,
            LegacyAnchor.AddDays(2),
            987.65m,
            432.10m);

        return new LegacyDatabase(
            options,
            LegacyAnchor,
            candidates,
            candidates.Select(c => c.SaleId).ToArray(),
            legitimateSaleId,
            candidates.Select(c => c.SaleId).Append(legitimateSaleId).OrderBy(id => id).ToArray(),
            itemIds.OrderBy(id => id).ToArray());
    }

    private static async Task InsertLegacySaleAsync(
        NpgsqlConnection conn,
        Guid saleId,
        Guid itemId,
        Guid managerId,
        Guid customerId,
        Guid productId,
        DateTimeOffset occurredAt,
        decimal unitPrice,
        decimal unitCost)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sales.sales ("Id", manager_id, customer_id, occurred_at, status)
            VALUES (@sale_id, @manager_id, @customer_id, @occurred_at, 'Paid');
            INSERT INTO sales.sale_items ("Id", sale_id, product_id, quantity, unit_price, unit_cost)
            VALUES (@item_id, @sale_id, @product_id, 1, @unit_price, @unit_cost);
            """;
        cmd.Parameters.AddWithValue("sale_id", saleId);
        cmd.Parameters.AddWithValue("item_id", itemId);
        cmd.Parameters.AddWithValue("manager_id", managerId);
        cmd.Parameters.AddWithValue("customer_id", customerId);
        cmd.Parameters.AddWithValue("product_id", productId);
        cmd.Parameters.AddWithValue("occurred_at", occurredAt);
        cmd.Parameters.AddWithValue("unit_price", unitPrice);
        cmd.Parameters.AddWithValue("unit_cost", unitCost);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task MigrateToLatestAsync(DbContextOptions<WriteDbContext> options)
    {
        await using var db = new WriteDbContext(options);
        await db.Database.MigrateAsync();
    }

    private static async Task<List<SaleSnapshot>> SalesSnapshotAsync(DbContextOptions<WriteDbContext> options)
    {
        await using var db = new WriteDbContext(options);
        return await db.Sales.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new SaleSnapshot(s.Id, s.ManagerId, s.CustomerId, s.OccurredAt, s.Status))
            .ToListAsync();
    }

    private static async Task<List<ItemSnapshot>> ItemsSnapshotAsync(DbContextOptions<WriteDbContext> options)
    {
        await using var db = new WriteDbContext(options);
        return await db.SaleItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new ItemSnapshot(i.Id, i.SaleId, i.ProductId, i.Quantity, i.UnitPrice, i.UnitCost))
            .ToListAsync();
    }

    private static async Task<List<ItemSnapshot>> ItemsForSaleSnapshotAsync(
        DbContextOptions<WriteDbContext> options,
        Guid saleId)
    {
        await using var db = new WriteDbContext(options);
        return await db.SaleItems.AsNoTracking().Where(i => i.SaleId == saleId).OrderBy(i => i.Id)
            .Select(i => new ItemSnapshot(i.Id, i.SaleId, i.ProductId, i.Quantity, i.UnitPrice, i.UnitCost))
            .ToListAsync();
    }
}
