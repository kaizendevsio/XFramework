using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Added_Correct_Props_PharmacyStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CriticalQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "CriticalQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "MaxQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "MinQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks");

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
                values: new object[] { new Guid("9d5539f0-44ae-4494-b330-6cb52cb87de0"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8384) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7aba8463-dcab-43b1-bb24-6d3415126b77"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8366) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("50b6ca67-8380-4339-8181-e3a982d600a2"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8386) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6d9401ac-7891-4950-9edd-a54f651ab540"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8359) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2299c1be-c2fb-4e69-b73a-848e9306f91f"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8364) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5f0f288b-1ee8-4af3-9e51-283cd795b4e8"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8382) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("415a7d72-2b8a-4ac7-870e-97f5a35fe556"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8378) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4180ded4-6116-4b6c-acd7-f1673c723204"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8388) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7ac04612-a5a1-4f9d-b786-fde565aec6ab"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8376) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("73b53949-7595-443c-99ce-357bde574901"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8362) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d72e23d3-77e6-4681-845b-36c6f2164cbd"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8369) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1148ee99-33e2-4c50-bdf4-b5cfc3fb3f9c"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8372) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("545b0cac-3ab0-47ee-99c1-16a5f49d7334"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8380) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f0c10f85-cbd9-4411-a16b-9dfb43c2560d"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8321) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("694447ac-e788-4aa9-8918-92e589d00ff0"), new DateTime(2024, 3, 29, 4, 5, 29, 964, DateTimeKind.Utc).AddTicks(8327) });
        }
    }
}
