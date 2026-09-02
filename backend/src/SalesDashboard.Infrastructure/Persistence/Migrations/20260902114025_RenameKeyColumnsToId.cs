using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDashboard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameKeyColumnsToId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "sales",
                table: "sales",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "sales",
                table: "sale_items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "catalog",
                table: "products",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "people",
                table: "managers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "people",
                table: "customers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "catalog",
                table: "categories",
                newName: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "id",
                schema: "sales",
                table: "sales",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "sales",
                table: "sale_items",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "catalog",
                table: "products",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "people",
                table: "managers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "people",
                table: "customers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "catalog",
                table: "categories",
                newName: "Id");
        }
    }
}
