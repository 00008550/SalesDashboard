using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDashboard.Modules.Catalog.Domain;
using SalesDashboard.Modules.Catalog.Persistence;
using SalesDashboard.Modules.People.Domain;
using SalesDashboard.Modules.People.Persistence;
using SalesDashboard.Modules.Sales.Domain;
using SalesDashboard.Modules.Sales.Persistence;

namespace SalesDashboard.Infrastructure.Persistence;

/// <summary>
/// The single write-side context. It deliberately aggregates the entity configurations that each
/// domain module owns (via <c>ApplyConfigurationsFromAssembly</c>) rather than defining mappings
/// here — so the modules keep ownership of their tables while the application keeps one migration
/// history and one <c>MigrateAsync</c> on startup (a Docker-startup simplification, documented in
/// README). The read side (Analytics) never uses this context; it reads through Dapper.
/// </summary>
public sealed class WriteDbContext(DbContextOptions<WriteDbContext> options) : DbContext(options)
{
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Manager> Managers => Set<Manager>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SeedMarker> SeedState => Set<SeedMarker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesSchema).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogSchema).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeopleSchema).Assembly);
        modelBuilder.ApplyConfiguration(new SeedMarkerConfiguration());

        // Cross-module relational integrity lives here: Infrastructure is the only place that sees
        // every module's entity types (the modules reference only Contracts, never each other). These
        // are id-only foreign keys — no navigations — so the module boundary in code is preserved
        // while PostgreSQL enforces referential integrity across schemas. Restrict everywhere: history
        // is never silently cascaded away.
        modelBuilder.Entity<Sale>()
            .HasOne<Manager>().WithMany().HasForeignKey(s => s.ManagerId)
            .HasConstraintName("fk_sales_manager").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Sale>()
            .HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId)
            .HasConstraintName("fk_sales_customer").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SaleItem>()
            .HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId)
            .HasConstraintName("fk_sale_items_product").OnDelete(DeleteBehavior.Restrict);
    }
}
