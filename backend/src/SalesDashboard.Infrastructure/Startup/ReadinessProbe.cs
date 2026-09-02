using Microsoft.EntityFrameworkCore;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;

namespace SalesDashboard.Infrastructure.Startup;

/// <summary>The outcome of a readiness evaluation: whether the app is ready plus a stable status token.</summary>
public readonly record struct ReadinessResult(bool Ready, string Status);

/// <summary>
/// The single source of truth for readiness, shared by the <c>/api/health/ready</c> endpoint and its
/// regression test. Readiness proves three live facts, in order:
///
/// <list type="number">
/// <item>PostgreSQL is reachable.</item>
/// <item>The database is at the latest migration — <b>no</b> migration known to the app is still
/// pending. Connectivity plus a seed marker is not enough: the seed marker predates
/// <c>RenameKeyColumnsToId</c>, so a database reverted to <c>InitialSchema</c> would still carry a v1
/// marker while <c>/api/dashboard</c> fails with <c>42703</c>. Requiring an empty pending set closes
/// that gap.</item>
/// <item>The expected seed version and its latest non-destructive repair are present.</item>
/// </list>
///
/// It re-evaluates on every probe, so it flips back to not-ready if PostgreSQL later becomes
/// unavailable or the schema is rolled back, rather than latching at ready.
/// </summary>
public static class ReadinessProbe
{
    public static async Task<ReadinessResult> EvaluateAsync(WriteDbContext db, CancellationToken ct = default)
    {
        if (!await db.Database.CanConnectAsync(ct))
            return new ReadinessResult(false, "db_unreachable");

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

        return new ReadinessResult(true, "ready");
    }
}
