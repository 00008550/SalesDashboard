using Microsoft.EntityFrameworkCore;
using Npgsql;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;

namespace SalesDashboard.Infrastructure.Startup;

/// <summary>The outcome of a readiness evaluation: whether the app is ready plus a stable status token.</summary>
public readonly record struct ReadinessResult(bool Ready, string Status);

/// <summary>
/// The single source of truth for readiness, shared by the <c>/api/health/ready</c> endpoint and its
/// regression test. Readiness proves four live facts, in order:
///
/// <list type="number">
/// <item>PostgreSQL is reachable.</item>
/// <item>The database is at the latest migration — <b>no</b> migration known to the app is still
/// pending. Connectivity plus a seed marker is not enough: the seed marker predates
/// <c>RenameKeyColumnsToId</c>, so a database reverted to <c>InitialSchema</c> would still carry a v1
/// marker while <c>/api/dashboard</c> fails with <c>42703</c>. Requiring an empty pending set closes
/// that gap.</item>
/// <item>The expected seed version and its latest non-destructive repair are present.</item>
/// <item>The pooled read path used by the dashboard can execute a command. A database restart can
/// leave idle connectors in the shared pool; a failed check clears them before readiness may
/// recover, so the first dashboard request does not inherit a stale connector.</item>
/// </list>
///
/// It re-evaluates on every probe, so it flips back to not-ready if PostgreSQL later becomes
/// unavailable or the schema is rolled back, rather than latching at ready.
/// </summary>
public static class ReadinessProbe
{
    public static async Task<ReadinessResult> EvaluateAsync(
        WriteDbContext db,
        NpgsqlDataSource readDataSource,
        CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return DatabaseUnreachable(readDataSource);

            // Any migration the app knows about that the database has not applied — including the latest —
            // leaves the schema behind the code. That is not ready, even if a prior seed marker exists.
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            if (pending.Any())
                return new ReadinessResult(false, "migrations_pending");

            var marker = await db.SeedState.SingleOrDefaultAsync(m => m.Id == 1, ct);
            if (marker is null || marker.Version != DeterministicSeeder.SeedVersion)
                return new ReadinessResult(false, "not_seeded");
            if (marker.RepairVersion < DeterministicSeeder.LatestRepairVersion)
                return new ReadinessResult(false, "seed_repair_pending");

            await using var connection = await readDataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            if (!Equals(await command.ExecuteScalarAsync(ct), 1))
                return DatabaseUnreachable(readDataSource);

            return new ReadinessResult(true, "ready");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (NpgsqlException)
        {
            // PostgreSQL sends 57P01 when it shuts down. A connector can remain apparently idle in
            // this pool until its next command observes that termination; clear the whole pool so a
            // later 200 is backed by fresh connections rather than leaving the failure for a user.
            return DatabaseUnreachable(readDataSource);
        }
    }

    private static ReadinessResult DatabaseUnreachable(NpgsqlDataSource dataSource)
    {
        dataSource.Clear();
        return new ReadinessResult(false, "db_unreachable");
    }
}
