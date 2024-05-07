using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Added_Wallet_RunningAvailableBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RunningAvailableBalance",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunningAvailableBalance",
                schema: "Wallet",
                table: "WalletTransaction");
        }
    }
}
