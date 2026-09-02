using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Tests.Fixtures;

namespace SalesDashboard.Tests.Startup;

/// <summary>
/// Drives the real API startup pipeline (migration -> seed -> readiness) against a fresh empty
/// PostgreSQL database, then asserts the app reports ready and the database is populated (inspected
/// directly, not via a production endpoint).
/// </summary>
[Collection("postgres")]
public sealed class StartupTests(PostgresFixture fx)
{
    [Fact]
    public async Task App_migrates_seeds_and_reports_ready_with_populated_data()
    {
        var connectionString = await fx.NewEmptyDatabaseConnectionStringAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

            using var client = factory.CreateClient(); // building the server runs migrate + seed

            var ready = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

            // Inspect the database directly to confirm the seed populated it.
            await using var db = new WriteDbContext(PostgresFixture.OptionsFor(connectionString));
            var sales = await db.Sales.CountAsync();
            Assert.InRange(sales, 2000, 5000);
            Assert.True(await db.SaleItems.CountAsync() > sales, "multi-item sales mean more items than sales");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        }
    }
}
