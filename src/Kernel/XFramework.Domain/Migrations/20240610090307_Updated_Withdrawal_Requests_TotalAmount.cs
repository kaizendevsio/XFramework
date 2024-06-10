using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Withdrawal_Requests_TotalAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                schema: "Wallet",
                table: "WithdrawalRequest",
                newName: "Amount");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "Fee",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletTypeId",
                principalSchema: "Wallet",
                principalTable: "WalletType",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "Fee",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.RenameColumn(
                name: "Amount",
                schema: "Wallet",
                table: "WithdrawalRequest",
                newName: "TotalAmount");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletTypeId",
                principalSchema: "Wallet",
                principalTable: "WalletType",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
