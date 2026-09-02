using Microsoft.EntityFrameworkCore;
using SalesDashboard.Api;
using SalesDashboard.Infrastructure;
using SalesDashboard.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is required (set ConnectionStrings__Default for the container).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<StartupState>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Liveness: the process is up. Never fails — used for restart decisions, not readiness.
app.MapGet("/api/health/live", () => Results.Ok(new { status = "live" }));

// Readiness: true only once migrations + seed have completed. Drives the Docker health check and
// any depends_on: service_healthy gate.
app.MapGet("/api/health/ready", (StartupState state) => state.Ready
    ? Results.Ok(new { status = "ready" })
    : Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable));

// Temporary proof-of-data endpoint for the early Docker vertical slice; replaced by /api/dashboard
// in the analytics milestone. Confirms the seed actually populated the database.
app.MapGet("/api/meta/counts", async (WriteDbContext db, CancellationToken ct) => Results.Ok(new
{
    categories = await db.Categories.CountAsync(ct),
    products = await db.Products.CountAsync(ct),
    managers = await db.Managers.CountAsync(ct),
    customers = await db.Customers.CountAsync(ct),
    sales = await db.Sales.CountAsync(ct),
    saleItems = await db.SaleItems.CountAsync(ct),
}));

// Startup order: migrate → seed → mark ready. Runs before the app serves, so the port opens (and
// readiness returns 200) only once the database is fully prepared.
await app.Services.MigrateAndSeedAsync();
app.Services.GetRequiredService<StartupState>().Ready = true;

app.Run();

// Exposed so WebApplicationFactory-based tests can drive the real startup pipeline.
public partial class Program;
