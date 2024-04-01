using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_Pharmacy_Stock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Duration",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "numeric(2)",
                precision: 2,
                nullable: false,
                defaultValueSql: "1",
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2,
                oldDefaultValueSql: "1");

            migrationBuilder.AddColumn<Guid>(
                name: "PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Duration",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "numeric(2)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2);

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("643b6c22-17c5-4f92-b475-5f78467376a9"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7982) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("30556a97-a31d-477d-8678-5898f249a2e0"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7964) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b1dbac3e-7a5e-4010-930b-6929150eba20"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7984) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("126d97ae-4077-4501-99a9-8d514133103d"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7946) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6796cf12-9d81-4fd9-911b-c440c63b130f"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7962) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("18118902-55d4-4851-92e0-c0aee1534567"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7979) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("26b5e5cc-0e80-4d19-a7b5-12e1635ff4e5"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7973) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4104e387-2d07-4291-bbe4-79199a1cf6b4"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7986) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4e1dd6d8-d42c-4a16-9050-a71f6f1909f8"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7971) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("499797b5-23c7-4a9a-b136-99c98398b519"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7960) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("571e0800-06cf-4a2f-9579-92b900a36cd5"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7967) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("df634e0f-4926-4730-b58d-6e13bf340780"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7969) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ea8bc18b-7629-47bd-89d7-b1da1a5a3cca"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7976) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3703ab1c-48d2-4b12-a95e-06590375b78c"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7913) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f072bc45-d395-4cc9-af89-495b14b4aa68"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7917) });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineVariant_PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant",
                column: "PharmacyStockId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicineVariant_PharmacyStocks_PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant",
                column: "PharmacyStockId",
                principalSchema: "Pharmacy",
                principalTable: "PharmacyStocks",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicineVariant_PharmacyStocks_PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant");

            migrationBuilder.DropIndex(
                name: "IX_MedicineVariant_PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant");

            migrationBuilder.DropColumn(
                name: "PharmacyStockId",
                schema: "Medicine",
                table: "MedicineVariant");

            migrationBuilder.AlterColumn<decimal>(
                name: "Duration",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                defaultValueSql: "1",
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2,
                oldDefaultValueSql: "1");

            migrationBuilder.AlterColumn<decimal>(
                name: "Duration",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2);

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("00e484cb-dff9-44a1-9ad3-b654ba052336"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9748) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3e0f7839-9bfe-472a-a35e-bd102034696e"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9730) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("03f275f4-306e-470b-b738-092be9ab3ee5"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9750) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("53b4d621-1f37-4a82-a0f7-4aee62a33c73"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9722) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8b944a16-733d-4840-b32e-bb94c1cea3a5"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9728) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fc72c151-0376-4aaa-8346-876a95a45b99"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9746) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("94e996c1-87b6-4bd4-a335-501a690503e4"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9741) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a55d9edb-6c71-41da-9468-06a7a0060d1a"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9752) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("70637f3a-8fe3-4b9d-afad-915317a43489"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9739) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("71c66cb2-c3a1-49e0-a54e-34c4cebb273f"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9725) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("afffd009-70cb-4d38-a8b1-caae0d76537e"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9733) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("dc609f72-1634-4358-a045-7586e6aa1769"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9735) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fb9fd2d1-0f4c-4c3e-aa07-e862040a5a36"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9743) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cf6c8258-1544-4ade-948d-f3a4234141df"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9686) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a7e8072f-210d-4eaf-9ab7-7e831c69491d"), new DateTime(2024, 3, 29, 4, 21, 45, 909, DateTimeKind.Utc).AddTicks(9690) });
        }
    }
}
