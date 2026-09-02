namespace SalesDashboard.Api;

/// <summary>
/// Tracks whether startup preparation (migrations + seed) has completed. Readiness reflects this,
/// not just a reachable Postgres — the Docker health check must not report the API ready before the
/// database is migrated and seeded.
/// </summary>
public sealed class StartupState
{
    private volatile bool _ready;

    public bool Ready
    {
        get => _ready;
        set => _ready = value;
    }
}
