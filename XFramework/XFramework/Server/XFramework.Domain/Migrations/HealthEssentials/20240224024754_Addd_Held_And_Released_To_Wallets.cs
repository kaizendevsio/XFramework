using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class AdddHeldAndReleasedToWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OnHold",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "Released");

            migrationBuilder.AddColumn<bool>(
                name: "Held",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Held",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.RenameColumn(
                name: "Released",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "OnHold");
        }
    }
}
