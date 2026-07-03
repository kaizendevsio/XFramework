using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class POSProductionHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "Inventario",
                table: "Reservation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "POS",
                table: "PosSale",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "POS",
                table: "PosReturn",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Inventario_Reservation_Tenant_Idempotency_Active",
                schema: "Inventario",
                table: "Reservation",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Inventario_Reservation_Tenant_Idempotency_Active",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "POS",
                table: "PosSale");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "POS",
                table: "PosReturn");
        }
    }
}
