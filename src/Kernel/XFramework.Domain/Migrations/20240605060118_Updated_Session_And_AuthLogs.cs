using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Session_And_AuthLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AuthStatus",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "integer",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceAgent",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceAgent",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.AlterColumn<short>(
                name: "AuthStatus",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
