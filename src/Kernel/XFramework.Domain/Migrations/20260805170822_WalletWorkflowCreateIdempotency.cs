using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class WalletWorkflowCreateIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_TenantId_IdempotencyKey",
                schema: "Wallet",
                table: "WithdrawalRequest",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_TenantId_IdempotencyKey",
                schema: "Wallet",
                table: "DepositRequest",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_TenantId_IdempotencyKey",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_TenantId_IdempotencyKey",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "Wallet",
                table: "DepositRequest");
        }
    }
}
