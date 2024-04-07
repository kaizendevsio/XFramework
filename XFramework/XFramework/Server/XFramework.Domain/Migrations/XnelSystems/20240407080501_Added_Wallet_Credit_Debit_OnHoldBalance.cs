using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Added_Wallet_Credit_Debit_OnHoldBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RunningOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "RunningDebitOnHoldBalance");

            migrationBuilder.RenameColumn(
                name: "PreviousOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "PreviousDebitOnHoldBalance");

            migrationBuilder.RenameColumn(
                name: "OnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                newName: "DebitOnHoldBalance");

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousCreditOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RunningCreditOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditOnHoldBalance",
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
                name: "PreviousCreditOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "RunningCreditOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "CreditOnHoldBalance",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.RenameColumn(
                name: "RunningDebitOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "RunningOnHoldBalance");

            migrationBuilder.RenameColumn(
                name: "PreviousDebitOnHoldBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "PreviousOnHoldBalance");

            migrationBuilder.RenameColumn(
                name: "DebitOnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                newName: "OnHoldBalance");
        }
    }
}
