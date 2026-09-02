using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDashboard.Contracts;
using SalesDashboard.Modules.Sales.Domain;

namespace SalesDashboard.Modules.Sales.Persistence;

/// <summary>Names the module owns: its PostgreSQL schema and the read-contract table names Analytics
/// depends on. Keeping them here (not in Analytics) means the domain module owns its data contract.</summary>
public static class SalesSchema
{
    public const string Name = "sales";
    public const string SalesTable = "sales";
    public const string SaleItemsTable = "sale_items";
}

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable(SalesSchema.SalesTable, SalesSchema.Name);
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id");

        b.Property(s => s.OccurredAt).HasColumnName("occurred_at").IsRequired();
        b.Property(s => s.ManagerId).HasColumnName("manager_id").IsRequired();
        b.Property(s => s.CustomerId).HasColumnName("customer_id").IsRequired();

        // Persist the status as text so analytical SQL reads WHERE status = 'Paid'.
        b.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .HasConstraintName("fk_sale_items_sale")
            .OnDelete(DeleteBehavior.Restrict);

        // The dashboard filters and groups by date, almost always constrained to Paid rows. A
        // composite (status, occurred_at) index serves the hot path: status equality + a range scan.
        b.HasIndex(s => new { s.Status, s.OccurredAt }).HasDatabaseName("ix_sales_status_occurred_at");
        b.HasIndex(s => s.OccurredAt).HasDatabaseName("ix_sales_occurred_at");
        b.HasIndex(s => s.ManagerId).HasDatabaseName("ix_sales_manager_id");
    }
}

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.ToTable(SalesSchema.SaleItemsTable, SalesSchema.Name, t =>
        {
            // Positive quantity; non-negative money. The one-sale grain relies on quantity > 0.
            t.HasCheckConstraint("ck_sale_items_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_sale_items_unit_price_nonneg", "unit_price >= 0");
            t.HasCheckConstraint("ck_sale_items_unit_cost_nonneg", "unit_cost >= 0");
        });
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("id");

        b.Property(i => i.SaleId).HasColumnName("sale_id").IsRequired();
        b.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();
        b.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();

        // money(18,2) — exact decimal, never float, for prices and costs.
        b.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
        b.Property(i => i.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2).IsRequired();

        // Aggregations join sale_items back to sales on sale_id and to products on product_id.
        b.HasIndex(i => i.SaleId).HasDatabaseName("ix_sale_items_sale_id");
        b.HasIndex(i => i.ProductId).HasDatabaseName("ix_sale_items_product_id");
    }
}
