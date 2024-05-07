using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Add_Wallet_Account_Number : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                schema: "Wallet",
                table: "Wallet",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardNumber",
                schema: "Wallet",
                table: "Wallet",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                schema: "Wallet",
                table: "Wallet");
        }
    }
}
