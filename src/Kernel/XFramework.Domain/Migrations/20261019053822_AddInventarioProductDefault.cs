using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioProductDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultProductAvailable",
                schema: "Inventario",
                table: "InventarioSetup",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            // Existing tenants retain the previous available-for-sale default.
            migrationBuilder.Sql("UPDATE \"Inventario\".\"InventarioSetup\" SET \"DefaultProductAvailable\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultProductAvailable",
                schema: "Inventario",
                table: "InventarioSetup");
        }
    }
}
