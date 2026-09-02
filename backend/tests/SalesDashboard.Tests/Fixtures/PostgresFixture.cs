using Microsoft.EntityFrameworkCore;
using Npgsql;
using SalesDashboard.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesDashboard.Tests.Fixtures;

/// <summary>
/// One shared PostgreSQL container for the whole test run (the plan calls for a single fixture).
/// Each test asks for its own freshly-migrated database on that server, so tests never share state
/// and the seed/analytics assertions run against the real migration on real PostgreSQL — SQLite or
/// the in-memory provider would not exercise the check constraints, foreign keys, or SQL the code
/// actually depends on.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates a brand-new empty database on the shared server and returns its connection string.</summary>
    public async Task<string> NewEmptyDatabaseConnectionStringAsync()
    {
        var dbName = "t" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var cmd = admin.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = dbName }.ConnectionString;
    }

    public static DbContextOptions<WriteDbContext> OptionsFor(string connectionString) =>
        new DbContextOptionsBuilder<WriteDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "public"))
            .Options;

    /// <summary>Creates a new database, applies the real migration, and returns options bound to it.</summary>
    public async Task<DbContextOptions<WriteDbContext>> NewMigratedDatabaseAsync()
    {
        var options = OptionsFor(await NewEmptyDatabaseConnectionStringAsync());
        await using var db = new WriteDbContext(options);
        await db.Database.MigrateAsync();
        return options;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
