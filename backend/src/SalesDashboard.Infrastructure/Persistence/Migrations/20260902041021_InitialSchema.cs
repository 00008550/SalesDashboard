using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDashboard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "people");

            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    company = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    segment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "managers",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    team = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    initials = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    avatar_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_managers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "seed_state",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seed_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    base_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.CheckConstraint("ck_products_base_cost_nonneg", "base_cost >= 0");
                    table.CheckConstraint("ck_products_base_price_nonneg", "base_price >= 0");
                    table.ForeignKey(
                        name: "fk_products_category",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.Id);
                    table.ForeignKey(
                        name: "fk_sales_customer",
                        column: x => x.customer_id,
                        principalSchema: "people",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_manager",
                        column: x => x.manager_id,
                        principalSchema: "people",
                        principalTable: "managers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_items", x => x.Id);
                    table.CheckConstraint("ck_sale_items_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_sale_items_unit_cost_nonneg", "unit_cost >= 0");
                    table.CheckConstraint("ck_sale_items_unit_price_nonneg", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_sale_items_product",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_items_sale",
                        column: x => x.sale_id,
                        principalSchema: "sales",
                        principalTable: "sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_categories_name",
                schema: "catalog",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                schema: "catalog",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_products_sku",
                schema: "catalog",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_product_id",
                schema: "sales",
                table: "sale_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_sale_id",
                schema: "sales",
                table: "sale_items",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_customer_id",
                schema: "sales",
                table: "sales",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_manager_id",
                schema: "sales",
                table: "sales",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_occurred_at",
                schema: "sales",
                table: "sales",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_sales_status_occurred_at",
                schema: "sales",
                table: "sales",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_items",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "seed_state",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "sales",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "people");

            migrationBuilder.DropTable(
                name: "managers",
                schema: "people");
        }
    }
}
