using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class InventarioBackendRemediation20260805 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.AlterColumn<decimal>(
                name: "StockQuantity",
                schema: "Inventario",
                table: "Product",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH ranked_rules AS (
                    SELECT "ID",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId"
                               ORDER BY "CreatedAt", "ID") AS duplicate_rank
                    FROM "Inventario"."InventoryReorderRule"
                    WHERE "IsDeleted" = false
                )
                UPDATE "Inventario"."InventoryReorderRule" AS rule
                SET "IsDeleted" = true,
                    "DeletedAt" = NOW(),
                    "ModifiedAt" = NOW()
                FROM ranked_rules
                WHERE rule."ID" = ranked_rules."ID"
                  AND ranked_rules.duplicate_rank > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "\"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.AlterColumn<int>(
                name: "StockQuantity",
                schema: "Inventario",
                table: "Product",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId" });
        }
    }
}
