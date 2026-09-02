using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SalesDashboard.Infrastructure;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;
using SalesDashboard.Infrastructure.Startup;
using SalesDashboard.Modules.Analytics;
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
        await using var readDataSource = NpgsqlDataSource.Create(connectionString);

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
            var stale = await ReadinessProbe.EvaluateAsync(db, readDataSource);
            Assert.False(stale.Ready);
            Assert.Equal("migrations_pending", stale.Status);
        }

        // Applying the schema migration exposes a separate required data-repair state. A v1 marker
        // is not ready until the one-time repair has completed.
        await using (var db = new WriteDbContext(opts))
            await db.Database.MigrateAsync();

        await using (var db = new WriteDbContext(opts))
        {
            var repairPending = await ReadinessProbe.EvaluateAsync(db, readDataSource);
            Assert.False(repairPending.Ready);
            Assert.Equal("seed_repair_pending", repairPending.Status);
        }

        await using (var db = new WriteDbContext(opts))
            await new DeterministicSeeder(db, TimeProvider.System, NullLogger<DeterministicSeeder>.Instance)
                .SeedAsync();

        await using (var db = new WriteDbContext(opts))
        {
            var repaired = await ReadinessProbe.EvaluateAsync(db, readDataSource);
            Assert.True(repaired.Ready);
            Assert.Equal("ready", repaired.Status);
        }
    }

    [Fact]
    public async Task Stale_pooled_connector_is_cleared_before_readiness_recovers()
    {
        var connectionString = await fx.NewEmptyDatabaseConnectionStringAsync();
        var pooledConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = 1,
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(pooledConnectionString);
        services.AddScoped<DashboardQueries>();
        services.AddScoped<DashboardService>();
        await using var provider = services.BuildServiceProvider();

        await provider.MigrateAndSeedAsync();
        var readDataSource = provider.GetRequiredService<NpgsqlDataSource>();

        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ReadinessProbe.EvaluateAsync(db, readDataSource, cancelled.Token));
        }

        int pooledBackendPid;
        await using (var connection = await readDataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT pg_backend_pid()";
            pooledBackendPid = (int)(await command.ExecuteScalarAsync())!;
        }

        // Kill the idle PostgreSQL backend behind the sole pooled connector. The connector remains
        // available for checkout until its next command observes the server-side termination.
        await using (var admin = new NpgsqlConnection(fx.AdminConnectionString))
        await using (var terminate = admin.CreateCommand())
        {
            await admin.OpenAsync();
            terminate.CommandText = "SELECT pg_terminate_backend(@pid)";
            terminate.Parameters.AddWithValue("pid", pooledBackendPid);
            Assert.Equal(true, await terminate.ExecuteScalarAsync());
        }

        await using (var firstScope = provider.CreateAsyncScope())
        {
            var firstDb = firstScope.ServiceProvider.GetRequiredService<WriteDbContext>();
            var stale = await ReadinessProbe.EvaluateAsync(firstDb, readDataSource);
            Assert.False(stale.Ready);
            Assert.Equal("db_unreachable", stale.Status);
        }

        await using (var recoveredScope = provider.CreateAsyncScope())
        {
            var recoveredDb = recoveredScope.ServiceProvider.GetRequiredService<WriteDbContext>();
            var recovered = await ReadinessProbe.EvaluateAsync(recoveredDb, readDataSource);
            Assert.True(recovered.Ready);
            Assert.Equal("ready", recovered.Status);
        }

        // The next real dashboard composition uses the same data source and must succeed on its
        // first attempt after readiness reports 200.
        await using (var dashboardScope = provider.CreateAsyncScope())
        {
            var dashboard = dashboardScope.ServiceProvider.GetRequiredService<DashboardService>();
            var response = await dashboard.BuildAsync("last30", null, null, default);
            Assert.NotEmpty(response.Trend);
        }
    }
}
