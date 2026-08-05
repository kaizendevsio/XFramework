using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class HardenPosPaymentAndCartIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_POS_Payment_Tenant_Sale",
                schema: "POS",
                table: "PosPayment");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "POS",
                table: "PosCart",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_POS_Payment_Tenant_Sale_Active",
                schema: "POS",
                table: "PosPayment",
                columns: new[] { "TenantId", "SaleId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_POS_Payment_Tenant_Sale_Active",
                schema: "POS",
                table: "PosPayment");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "POS",
                table: "PosCart");

            migrationBuilder.CreateIndex(
                name: "IX_POS_Payment_Tenant_Sale",
                schema: "POS",
                table: "PosPayment",
                columns: new[] { "TenantId", "SaleId" });
        }
    }
}
