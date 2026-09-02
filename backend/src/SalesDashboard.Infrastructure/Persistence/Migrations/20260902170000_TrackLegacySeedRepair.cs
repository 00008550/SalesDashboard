using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDashboard.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gives the same-version legacy seed repair its own durable version. Existing v1 databases receive
/// zero and are inspected once after migration; fresh databases are seeded directly at the latest
/// repair version. The data repair itself remains in <c>DeterministicSeeder</c> because positive
/// identification requires replaying the original seeded RNG sequence.
/// </summary>
[DbContext(typeof(WriteDbContext))]
[Migration("20260902170000_TrackLegacySeedRepair")]
public partial class TrackLegacySeedRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "repair_version",
            schema: "ops",
            table: "seed_state",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "repair_version",
            schema: "ops",
            table: "seed_state");
    }
}
