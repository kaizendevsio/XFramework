using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_Units_PharmacyStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Unit",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "UnitId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyStocks_Unit",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "IX_PharmacyStocks_UnitId");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                schema: "Medicine",
                table: "MedicineVariant",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "Unit");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyStocks_UnitId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "IX_PharmacyStocks_Unit");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                schema: "Medicine",
                table: "MedicineVariant",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

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
                values: new object[] { new Guid("e9e802ba-97ec-4ffe-aa45-00d6e54a934f"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2446) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ad437fe3-b6b0-49fc-a98e-7472fdf7b230"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2430) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a4c58f84-a14f-4ecb-9a1d-0f5cb879b0e5"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2450) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cd2dec52-75d1-4739-82cd-712f47a519bb"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2414) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ce779b2f-e78e-4693-ace2-b1ba1bcba975"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2419) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0238b351-ab44-4e0e-9b1f-7db28d35dc63"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2444) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9f965433-5312-4d69-b19b-1c841f299668"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2439) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b67fdc3e-1bc3-4ef6-81b4-886ae483600d"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2452) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("efb82090-9cdb-4847-aca7-40fe24a4706d"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2437) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5626c5f7-cdd1-4442-aea1-ef6b19b2d18e"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2417) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a3c37037-26bb-432a-8704-b8e0db1ac62c"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2432) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("80f1b191-4784-4b6d-a462-ecd541724c19"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2435) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("358ee128-63ff-4e65-a5fc-26fcafa12c3e"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2442) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("79007c5e-6689-40d8-bfd7-3e859f597800"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2388) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("99a5812d-33a0-4cf5-9d3e-12b4ed478ca6"), new DateTime(2024, 3, 29, 4, 6, 15, 773, DateTimeKind.Utc).AddTicks(2392) });
        }
    }
}
