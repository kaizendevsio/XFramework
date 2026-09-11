using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class POSCashTenderAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashTenderedAmount",
                schema: "POS",
                table: "PosPayment",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                schema: "POS",
                table: "PosPayment",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashTenderedAmount",
                schema: "POS",
                table: "PosPayment");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                schema: "POS",
                table: "PosPayment");
        }
    }
}
