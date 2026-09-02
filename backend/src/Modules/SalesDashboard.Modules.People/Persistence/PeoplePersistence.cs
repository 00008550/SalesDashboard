using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDashboard.Modules.People.Domain;

namespace SalesDashboard.Modules.People.Persistence;

public static class PeopleSchema
{
    public const string Name = "people";
    public const string ManagersTable = "managers";
    public const string CustomersTable = "customers";
}

public sealed class ManagerConfiguration : IEntityTypeConfiguration<Manager>
{
    public void Configure(EntityTypeBuilder<Manager> b)
    {
        b.ToTable(PeopleSchema.ManagersTable, PeopleSchema.Name);
        b.HasKey(m => m.Id);
        b.Property(m => m.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
        b.Property(m => m.Title).HasColumnName("title").HasMaxLength(80).IsRequired();
        b.Property(m => m.Team).HasColumnName("team").HasMaxLength(80).IsRequired();
        b.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(m => m.Initials).HasColumnName("initials").HasMaxLength(4).IsRequired();
        b.Property(m => m.AvatarColor).HasColumnName("avatar_color").HasMaxLength(9).IsRequired();
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable(PeopleSchema.CustomersTable, PeopleSchema.Name);
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        b.Property(c => c.Company).HasColumnName("company").HasMaxLength(120).IsRequired();
        b.Property(c => c.Segment)
            .HasColumnName("segment")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
    }
}
