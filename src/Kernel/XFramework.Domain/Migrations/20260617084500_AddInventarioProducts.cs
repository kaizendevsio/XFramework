using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617084500_AddInventarioProducts")]
    public partial class AddInventarioProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Inventario");

            migrationBuilder.CreateTable(
                name: "Product",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SKU = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: true),
                    Rating = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Reviews = table.Column<List<string>>(type: "text[]", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_Product_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategory",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_ProductCategory_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProductTransaction",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_ProductTransaction_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariation",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdditionalPrice = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_ProductVariation_pkey", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Product_CategoryId",
                schema: "Inventario",
                table: "Product",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Product_SKU",
                schema: "Inventario",
                table: "Product",
                column: "SKU");

            migrationBuilder.CreateIndex(
                name: "IX_Product_TenantId",
                schema: "Inventario",
                table: "Product",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategory_Name",
                schema: "Inventario",
                table: "ProductCategory",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategory_TenantId",
                schema: "Inventario",
                table: "ProductCategory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTransaction_ProductId",
                schema: "Inventario",
                table: "ProductTransaction",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTransaction_TenantId",
                schema: "Inventario",
                table: "ProductTransaction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariation_ProductId",
                schema: "Inventario",
                table: "ProductVariation",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariation_TenantId",
                schema: "Inventario",
                table: "ProductVariation",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Product",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "ProductCategory",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "ProductTransaction",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "ProductVariation",
                schema: "Inventario");
        }
    }
}
