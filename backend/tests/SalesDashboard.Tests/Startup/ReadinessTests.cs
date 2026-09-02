using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace SalesDashboard.Tests.Startup;

/// <summary>
/// Readiness must reflect the live state of its dependencies, not a latched boolean. This test uses
/// its own disposable PostgreSQL container so it can stop it mid-flight without affecting other tests.
///
/// It joins the "postgres" collection purely to serialize it against <see cref="StartupTests"/>: both
/// drive <c>WebApplicationFactory</c> through the process-global <c>ConnectionStrings__Default</c>
/// environment variable, so running them in parallel would let one clobber the other's connection
/// string. Sharing a non-parallel collection removes that race without any shared environment state.
/// </summary>
[Collection("postgres")]
public sealed class ReadinessTests
{
    [Fact]
    public async Task Readiness_is_200_when_seeded_and_flips_to_503_when_the_database_stops()
    {
        await using var pg = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await pg.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", pg.GetConnectionString());
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

            using var client = factory.CreateClient(); // migrate + seed against the live container

            var ready = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

            // Prime the shared NpgsqlDataSource pool used by Dapper. PostgreSQL shutdown leaves this
            // idle connector stale until a later command observes 57P01.
            var initialDashboard = await client.GetAsync("/api/dashboard?preset=last30");
            Assert.Equal(HttpStatusCode.OK, initialDashboard.StatusCode);

            await pg.StopAsync(); // dependency loss

            var afterLoss = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, afterLoss.StatusCode);

            // Liveness stays up regardless of the database.
            var live = await client.GetAsync("/api/health/live");
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
            await pg.DisposeAsync();
        }
    }
}
