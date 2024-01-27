using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class UpdatedDataSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("3afe8862-9881-49b7-885c-fbe96544e07d"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("67ab57af-d5ce-4562-8797-3b53e0b42221"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("9f6cdc72-8ee3-4934-88c0-6aba12c69bbf"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("f9da3fec-e466-413a-b88e-adbbc26cff74"));

            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "numeric(2)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityContactGroup",
                columns: new[] { "ID", "ConcurrencyStamp", "DeletedAt", "IsDeleted", "IsEnabled", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "WORK", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "HOME", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"));

            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "numeric(2,0)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityAddressType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3afe8862-9881-49b7-885c-fbe96544e07d"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_RIDER", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "HOME", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("67ab57af-d5ce-4562-8797-3b53e0b42221"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_DOCTOR", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9f6cdc72-8ee3-4934-88c0-6aba12c69bbf"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_LABORATORY", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f9da3fec-e466-413a-b88e-adbbc26cff74"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_PHARMACY", new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }
    }
}
