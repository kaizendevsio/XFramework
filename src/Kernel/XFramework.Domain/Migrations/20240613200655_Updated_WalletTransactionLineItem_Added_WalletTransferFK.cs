using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_WalletTransactionLineItem_Added_WalletTransferFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                schema: "Wallet",
                table: "WalletTransactionLineItem");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransfer_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransactionId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransfer_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransferId",
                principalSchema: "Wallet",
                principalTable: "WalletTransfer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                schema: "Wallet",
                table: "WalletTransactionLineItem");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransfer_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransactionId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransfer_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransferId",
                principalSchema: "Wallet",
                principalTable: "WalletTransfer",
                principalColumn: "Id");
        }
    }
}
