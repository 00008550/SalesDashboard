using Microsoft.EntityFrameworkCore;
using SalesDashboard.Infrastructure;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;
using SalesDashboard.Infrastructure.Startup;
using SalesDashboard.Modules.Analytics;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is required (set ConnectionStrings__Default for the container).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<DashboardQueries>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Liveness: the process is up. Never touches dependencies — used for restart decisions only.
app.MapGet("/api/health/live", () => Results.Ok(new { status = "live" }));

// Readiness: a live check, re-evaluated on every probe. It proves the database is reachable, at the
// latest migration (no migration still pending), seeded, and usable through the pooled dashboard
// read path — see ReadinessProbe. It therefore flips back to 503 if PostgreSQL later becomes
// unavailable or the schema is rolled back, rather than latching at 200.
app.MapGet("/api/health/ready", async (WriteDbContext db, Npgsql.NpgsqlDataSource readDataSource, CancellationToken ct) =>
{
    try
    {
        var result = await ReadinessProbe.EvaluateAsync(db, readDataSource, ct);
        return result.Ready
            ? Results.Ok(new { status = result.Status })
            : Results.Json(new { status = result.Status }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch
    {
        return Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// The single composed dashboard snapshot. Accepts either ?preset=... or ?from=&to= (inclusive
// date-only) — mixing them, or an invalid/inverted range, is a 400 ProblemDetails. All aggregation
// happens server-side.
app.MapGet("/api/dashboard", async (
    string? preset, DateOnly? from, DateOnly? to, DashboardService dashboard, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await dashboard.BuildAsync(preset, from, to, ct));
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(title: "Invalid period", detail: ex.Message,
            statusCode: StatusCodes.Status400BadRequest);
    }
});

// Startup order: migrate → seed, before the app serves. The port opens only once the database is
// migrated and seeded, so readiness answers truthfully from the first request.
await app.Services.MigrateAndSeedAsync();

app.Run();

// Exposed so WebApplicationFactory-based tests can drive the real startup pipeline.
public partial class Program;
