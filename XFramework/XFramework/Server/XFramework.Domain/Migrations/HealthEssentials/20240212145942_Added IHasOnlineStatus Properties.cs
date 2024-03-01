using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class AddedIHasOnlineStatusProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Gender",
                schema: "Identity",
                table: "IdentityInformation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "CivilStatus",
                schema: "Identity",
                table: "IdentityInformation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Device",
                schema: "Identity",
                table: "IdentityCredential",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                schema: "Identity",
                table: "IdentityCredential",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastActivityType",
                schema: "Identity",
                table: "IdentityCredential",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Identity",
                table: "IdentityCredential",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "Identity",
                table: "IdentityCredential",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineSince",
                schema: "Identity",
                table: "IdentityCredential",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusMessage",
                schema: "Identity",
                table: "IdentityCredential",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Device",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "LastActivityType",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "OnlineSince",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "StatusMessage",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.AlterColumn<short>(
                name: "Gender",
                schema: "Identity",
                table: "IdentityInformation",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "CivilStatus",
                schema: "Identity",
                table: "IdentityInformation",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
