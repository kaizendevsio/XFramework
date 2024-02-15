using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class UpdatedDataSeedsIdentityAddressType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                table: "IdentityAddressType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("23c13259-1e24-427d-ba89-a4d2506c7464"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("337ee33d-445f-4e6e-bc61-8709170b0ee4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "SHIPPING", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4eec62eb-08ef-406c-9ea2-2ac2d6e0f206"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BILLING", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("54ab2c38-be75-4572-916b-72019d676162"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("66c8ab89-f24d-4aea-af1a-9ac6a8263575"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c9136227-f5dc-4147-984d-70aa855090e4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "HOME", new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("23c13259-1e24-427d-ba89-a4d2506c7464"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("337ee33d-445f-4e6e-bc61-8709170b0ee4"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("4eec62eb-08ef-406c-9ea2-2ac2d6e0f206"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("54ab2c38-be75-4572-916b-72019d676162"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("66c8ab89-f24d-4aea-af1a-9ac6a8263575"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("c9136227-f5dc-4147-984d-70aa855090e4"));

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
        }
    }
}
