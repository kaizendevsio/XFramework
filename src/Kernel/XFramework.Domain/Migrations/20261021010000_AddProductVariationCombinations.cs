using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261021010000_AddProductVariationCombinations")]
public sealed class AddProductVariationCombinations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CombinationOptions", schema: "Inventario",
            table: "ProductVariation", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CombinationKey", schema: "Inventario",
            table: "ProductVariation", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.CreateIndex(name: "UX_ProductVariation_Tenant_Product_Combination",
            schema: "Inventario", table: "ProductVariation",
            columns: ["TenantId", "ProductId", "CombinationKey"], unique: true,
            filter: "\"IsDeleted\" = false AND \"CombinationKey\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "UX_ProductVariation_Tenant_Product_Combination",
            schema: "Inventario", table: "ProductVariation");
        migrationBuilder.DropColumn(name: "CombinationKey", schema: "Inventario", table: "ProductVariation");
        migrationBuilder.DropColumn(name: "CombinationOptions", schema: "Inventario", table: "ProductVariation");
    }
}
