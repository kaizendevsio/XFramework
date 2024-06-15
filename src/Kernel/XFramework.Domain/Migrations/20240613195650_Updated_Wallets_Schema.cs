using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Wallets_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransaction_WalletTransact~",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransfers_WalletTransferId",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransfers_WalletTransaction_RecipientTransactionId",
                table: "WalletTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransfers_WalletTransaction_SenderTransactionId",
                table: "WalletTransfers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransfers",
                table: "WalletTransfers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransactionLineItems",
                table: "WalletTransactionLineItems");

            migrationBuilder.RenameTable(
                name: "WalletTransfers",
                newName: "WalletTransfer",
                newSchema: "Wallet");

            migrationBuilder.RenameTable(
                name: "WalletTransactionLineItems",
                newName: "WalletTransactionLineItem",
                newSchema: "Wallet");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransfers_SenderTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                newName: "IX_WalletTransfer_SenderTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransfers_RecipientTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                newName: "IX_WalletTransfer_RecipientTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItems_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                newName: "IX_WalletTransactionLineItem_WalletTransferId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItems_WalletTransactionId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                newName: "IX_WalletTransactionLineItem_WalletTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransfer",
                schema: "Wallet",
                table: "WalletTransfer",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransactionLineItem",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransfer_WalletTransaction_RecipientTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                column: "RecipientTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransfer_WalletTransaction_SenderTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                column: "SenderTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
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

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransfer_WalletTransaction_RecipientTransactionId",
                schema: "Wallet",
                table: "WalletTransfer");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransfer_WalletTransaction_SenderTransactionId",
                schema: "Wallet",
                table: "WalletTransfer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransfer",
                schema: "Wallet",
                table: "WalletTransfer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransactionLineItem",
                schema: "Wallet",
                table: "WalletTransactionLineItem");

            migrationBuilder.RenameTable(
                name: "WalletTransfer",
                schema: "Wallet",
                newName: "WalletTransfers");

            migrationBuilder.RenameTable(
                name: "WalletTransactionLineItem",
                schema: "Wallet",
                newName: "WalletTransactionLineItems");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransfer_SenderTransactionId",
                table: "WalletTransfers",
                newName: "IX_WalletTransfers_SenderTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransfer_RecipientTransactionId",
                table: "WalletTransfers",
                newName: "IX_WalletTransfers_RecipientTransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItem_WalletTransferId",
                table: "WalletTransactionLineItems",
                newName: "IX_WalletTransactionLineItems_WalletTransferId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItem_WalletTransactionId",
                table: "WalletTransactionLineItems",
                newName: "IX_WalletTransactionLineItems_WalletTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransfers",
                table: "WalletTransfers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransactionLineItems",
                table: "WalletTransactionLineItems",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransaction_WalletTransact~",
                table: "WalletTransactionLineItems",
                column: "WalletTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransfers_WalletTransferId",
                table: "WalletTransactionLineItems",
                column: "WalletTransferId",
                principalTable: "WalletTransfers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransfers_WalletTransaction_RecipientTransactionId",
                table: "WalletTransfers",
                column: "RecipientTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransfers_WalletTransaction_SenderTransactionId",
                table: "WalletTransfers",
                column: "SenderTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
