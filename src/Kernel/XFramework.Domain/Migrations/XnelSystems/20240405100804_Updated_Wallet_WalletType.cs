using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Updated_Wallet_WalletType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinTransfer",
                schema: "Wallet",
                table: "WalletType",
                newName: "MinTransferRule");

            migrationBuilder.RenameColumn(
                name: "MaxTransfer",
                schema: "Wallet",
                table: "WalletType",
                newName: "MaxTransferRule");

            migrationBuilder.AddColumn<decimal>(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "WalletType",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "WalletType",
                type: "numeric",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferableBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "MaxTransferRule",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "MinTransferRule",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "TransferableBalance",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.RenameColumn(
                name: "MinTransferRule",
                schema: "Wallet",
                table: "WalletType",
                newName: "MinTransfer");

            migrationBuilder.RenameColumn(
                name: "MaxTransferRule",
                schema: "Wallet",
                table: "WalletType",
                newName: "MaxTransfer");

            migrationBuilder.AlterColumn<decimal>(
                name: "OnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8);
        }
    }
}
