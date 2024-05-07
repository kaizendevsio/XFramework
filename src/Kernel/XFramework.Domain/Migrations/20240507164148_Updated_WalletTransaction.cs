using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_WalletTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConvenienceFee",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFee",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvenienceFee",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "TransactionFee",
                schema: "Wallet",
                table: "WalletTransaction");
        }
    }
}
