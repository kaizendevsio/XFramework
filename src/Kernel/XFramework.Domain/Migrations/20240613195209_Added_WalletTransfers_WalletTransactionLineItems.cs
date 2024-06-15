using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Added_WalletTransfers_WalletTransactionLineItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                table: "WalletTransactionLineItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransactionLineItem",
                table: "WalletTransactionLineItem");

            migrationBuilder.RenameTable(
                name: "WalletTransactionLineItem",
                newName: "WalletTransactionLineItems");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItem_WalletTransactionId",
                table: "WalletTransactionLineItems",
                newName: "IX_WalletTransactionLineItems_WalletTransactionId");

            migrationBuilder.AddColumn<Guid>(
                name: "WalletTransferId",
                table: "WalletTransactionLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransactionLineItems",
                table: "WalletTransactionLineItems",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "WalletTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionPurpose = table.Column<int>(type: "integer", nullable: false),
                    SenderTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "numeric", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransfers_WalletTransaction_RecipientTransactionId",
                        column: x => x.RecipientTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletTransfers_WalletTransaction_SenderTransactionId",
                        column: x => x.SenderTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactionLineItems_WalletTransferId",
                table: "WalletTransactionLineItems",
                column: "WalletTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransfers_RecipientTransactionId",
                table: "WalletTransfers",
                column: "RecipientTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransfers_SenderTransactionId",
                table: "WalletTransfers",
                column: "SenderTransactionId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransaction_WalletTransact~",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactionLineItems_WalletTransfers_WalletTransferId",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropTable(
                name: "WalletTransfers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletTransactionLineItems",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactionLineItems_WalletTransferId",
                table: "WalletTransactionLineItems");

            migrationBuilder.DropColumn(
                name: "WalletTransferId",
                table: "WalletTransactionLineItems");

            migrationBuilder.RenameTable(
                name: "WalletTransactionLineItems",
                newName: "WalletTransactionLineItem");

            migrationBuilder.RenameIndex(
                name: "IX_WalletTransactionLineItems_WalletTransactionId",
                table: "WalletTransactionLineItem",
                newName: "IX_WalletTransactionLineItem_WalletTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletTransactionLineItem",
                table: "WalletTransactionLineItem",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                table: "WalletTransactionLineItem",
                column: "WalletTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
