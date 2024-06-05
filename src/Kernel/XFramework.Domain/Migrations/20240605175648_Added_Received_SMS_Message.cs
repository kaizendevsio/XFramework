using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Added_Received_SMS_Message : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "WithdrawalStatus",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSender",
                schema: "Messaging",
                table: "MessageDirect",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSender",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.AlterColumn<short>(
                name: "WithdrawalStatus",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
