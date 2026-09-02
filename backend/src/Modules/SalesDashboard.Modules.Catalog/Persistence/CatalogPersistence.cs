using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDashboard.Modules.Catalog.Domain;

namespace SalesDashboard.Modules.Catalog.Persistence;

public static class CatalogSchema
{
    public const string Name = "catalog";
    public const string CategoriesTable = "categories";
    public const string ProductsTable = "products";
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable(CatalogSchema.CategoriesTable, CatalogSchema.Name);
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.HasIndex(c => c.Name).IsUnique().HasDatabaseName("ux_categories_name");
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable(CatalogSchema.ProductsTable, CatalogSchema.Name, t =>
        {
            // Monetary values are never negative. Enforced at the DBMS so no code path can violate it.
            t.HasCheckConstraint("ck_products_base_price_nonneg", "base_price >= 0");
            t.HasCheckConstraint("ck_products_base_cost_nonneg", "base_cost >= 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(40).IsRequired();
        b.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
        b.Property(p => p.BasePrice).HasColumnName("base_price").HasPrecision(18, 2).IsRequired();
        b.Property(p => p.BaseCost).HasColumnName("base_cost").HasPrecision(18, 2).IsRequired();

        // Product → Category (same module). Restrict: a category that is in use cannot be deleted.
        b.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId)
            .HasConstraintName("fk_products_category").OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => p.CategoryId).HasDatabaseName("ix_products_category_id");
        b.HasIndex(p => p.Sku).IsUnique().HasDatabaseName("ux_products_sku");
    }
}
