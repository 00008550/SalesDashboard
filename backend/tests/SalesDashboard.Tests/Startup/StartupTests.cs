using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using SalesDashboard.Tests.Fixtures;

namespace SalesDashboard.Tests.Startup;

/// <summary>
/// Drives the real API startup pipeline (migration -> seed -> readiness) against a fresh empty
/// PostgreSQL database, then asserts the app reports ready and the database is populated. This proves
/// the wiring the Docker vertical slice depends on, without Docker.
/// </summary>
[Collection("postgres")]
public sealed class StartupTests(PostgresFixture fx)
{
    private sealed record Counts(int categories, int products, int managers, int customers, int sales, int saleItems);

    [Fact]
    public async Task App_migrates_seeds_and_reports_ready_with_populated_data()
    {
        var connectionString = await fx.NewEmptyDatabaseConnectionStringAsync();

        // The connection string is read during WebApplication.CreateBuilder, before any per-factory
        // ConfigureAppConfiguration would apply, so supply it via the environment variable the app
        // already reads. Scoped to this test and cleared in the finally.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

            using var client = factory.CreateClient(); // building the server runs migrate + seed

            var ready = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

            var counts = await client.GetFromJsonAsync<Counts>("/api/meta/counts");
            Assert.NotNull(counts);
            Assert.InRange(counts!.sales, 2000, 5000);
            Assert.True(counts.saleItems > counts.sales, "multi-item sales mean more items than sales");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        }
    }
}
