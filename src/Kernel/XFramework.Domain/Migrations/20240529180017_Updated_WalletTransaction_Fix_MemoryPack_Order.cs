using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_WalletTransaction_Fix_MemoryPack_Order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ConvenienceFee",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "TotalFees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalFees",
                schema: "Wallet",
                table: "WalletTransaction",
                newName: "ConvenienceFee");
        }
    }
}
