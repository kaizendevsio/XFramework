using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class AddOnHoldTotalBalanceToWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "RunningBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8);

            migrationBuilder.AddColumn<bool>(
                name: "OnHold",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousTotalBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RunningOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RunningTotalBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnHold",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "PreviousOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "PreviousTotalBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "RunningOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "RunningTotalBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "OnHoldBalance",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.AlterColumn<decimal>(
                name: "RunningBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
