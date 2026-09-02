using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SalesDashboard.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without booting the API.
/// The connection string here is never connected to during <c>migrations add</c> — it only needs a
/// valid Npgsql string so the provider is configured. Runtime wiring will live in the API
/// composition root, which supplies the real connection string from configuration.
/// </summary>
public sealed class WriteDbContextFactory : IDesignTimeDbContextFactory<WriteDbContext>
{
    public WriteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WriteDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=salesdashboard;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "public"))
            .Options;

        return new WriteDbContext(options);
    }
}
