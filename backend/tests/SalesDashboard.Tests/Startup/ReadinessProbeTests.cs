using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;
using SalesDashboard.Infrastructure.Startup;
using SalesDashboard.Tests.Fixtures;

namespace SalesDashboard.Tests.Startup;

/// <summary>
/// Readiness must prove the schema is at the latest migration, not merely that the database is
/// reachable and a seed marker exists. A database stuck at <c>InitialSchema</c> (before
/// <c>RenameKeyColumnsToId</c>) is reachable and could carry a v1 seed marker, yet
/// <c>/api/dashboard</c> would fail with <c>42703</c> — so readiness must report not-ready there.
/// </summary>
[Collection("postgres")]
public sealed class ReadinessProbeTests(PostgresFixture fx)
{
    private const string InitialSchemaMigration = "20260902041021_InitialSchema";

    [Fact]
    public async Task Reachable_seeded_database_missing_the_latest_migration_is_not_ready()
    {
        var connectionString = await fx.NewEmptyDatabaseConnectionStringAsync();
        var opts = PostgresFixture.OptionsFor(connectionString);

        // Bring the database only up to InitialSchema, deliberately leaving RenameKeyColumnsToId pending.
        await using (var db = new WriteDbContext(opts))
        {
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(InitialSchemaMigration);
        }

        // Mark it "seeded" (v1) directly — the marker table exists at InitialSchema. This reproduces the
        // exact trap: reachable + seeded, but the schema is a migration behind the code.
        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO ops.seed_state (id, version, applied_at) VALUES (1, 1, now())";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var db = new WriteDbContext(opts))
        {
            var stale = await ReadinessProbe.EvaluateAsync(db);
            Assert.False(stale.Ready);
            Assert.Equal("migrations_pending", stale.Status);
        }

        // Applying the schema migration exposes a separate required data-repair state. A v1 marker
        // is not ready until the one-time repair has completed.
        await using (var db = new WriteDbContext(opts))
            await db.Database.MigrateAsync();

        await using (var db = new WriteDbContext(opts))
        {
            var repairPending = await ReadinessProbe.EvaluateAsync(db);
            Assert.False(repairPending.Ready);
            Assert.Equal("seed_repair_pending", repairPending.Status);
        }

        await using (var db = new WriteDbContext(opts))
            await new DeterministicSeeder(db, TimeProvider.System, NullLogger<DeterministicSeeder>.Instance)
                .SeedAsync();

        await using (var db = new WriteDbContext(opts))
        {
            var repaired = await ReadinessProbe.EvaluateAsync(db);
            Assert.True(repaired.Ready);
            Assert.Equal("ready", repaired.Status);
        }
    }
}
