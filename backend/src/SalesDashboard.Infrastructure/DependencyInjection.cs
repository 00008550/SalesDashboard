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

        // Write side: EF Core owns migrations and seeding. One context, one migration history.
        services.AddDbContext<WriteDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public")));

        services.AddScoped<DeterministicSeeder>();

        // Read side: a shared NpgsqlDataSource that Analytics opens connections from for Dapper reads.
        // Registered here (not in Contracts) so Analytics depends on the Npgsql primitive directly.
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));

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
