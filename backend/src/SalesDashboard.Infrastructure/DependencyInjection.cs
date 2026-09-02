using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Infrastructure.Seeding;

namespace SalesDashboard.Infrastructure;

/// <summary>Composition helpers for the write side and the startup migrate+seed path.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);

        // One shared data source means readiness exercises and, after an outage, clears the same
        // connection pool used by both EF's write/startup path and Dapper's dashboard reads.
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        // Write side: EF Core owns migrations and seeding. One context, one migration history.
        services.AddDbContext<WriteDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public")));

        services.AddScoped<DeterministicSeeder>();

        return services;
    }

    /// <summary>Applies migrations, then runs the deterministic seed. Call once at startup before serving.</summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        await db.Database.MigrateAsync(ct);
        await scope.ServiceProvider.GetRequiredService<DeterministicSeeder>().SeedAsync(ct);
    }
}
