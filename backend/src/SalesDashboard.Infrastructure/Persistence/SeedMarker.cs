using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SalesDashboard.Infrastructure.Persistence;

/// <summary>
/// A single-row marker (<c>id = 1</c>) recording which seed version has been applied. The seeder
/// reads it inside its transaction and skips when the stored version already matches, so running the
/// seed repeatedly — on every container start — never duplicates data, and bumping the seed version
/// re-seeds deterministically. This is the explicit idempotency mechanism the plan requires.
/// </summary>
public sealed class SeedMarker
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
}

public sealed class SeedMarkerConfiguration : IEntityTypeConfiguration<SeedMarker>
{
    public void Configure(EntityTypeBuilder<SeedMarker> b)
    {
        b.ToTable("seed_state", "ops");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Version).HasColumnName("version").IsRequired();
        b.Property(x => x.AppliedAt).HasColumnName("applied_at").IsRequired();
    }
}
