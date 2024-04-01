using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Removed_Incorrect_Props_PharmacyStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvailableQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CriticalQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MaxQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MinQuantity",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                values: new object[] { new Guid("8c2d3e5b-ab01-48e0-915f-89868070dbca"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2115) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("69b7186d-dde2-4fdb-9c97-aa71091ccfe6"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2098) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0b0c18d7-eb7f-4343-8262-b03243f69986"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2118) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7207fb73-60ca-44ab-96c0-b5f469076873"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2052) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d8779c0b-3d73-452c-8489-32554c5a2ef1"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2084) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2b9c6382-beb1-4b3e-85db-d44adb94c6e0"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2113) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e45e51cf-43db-4179-bf09-28b177b49da6"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2108) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("36b456f8-317d-4bc0-866c-a345ee76a822"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2121) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("00610f62-85f6-4728-a4fe-0fcfb4204d23"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2106) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bbaff277-29e1-45c2-a62d-ed1de9ec601c"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2082) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6ff3aeaa-9482-402d-94bc-4854ac350d19"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2101) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("771f8fb3-0445-4934-a8fb-1ccd9702a0fd"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2104) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0e2a047f-4158-4b78-97c8-819c7cf1caeb"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2110) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("80bb4321-b1f9-477c-a328-ab97edd1393a"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2021) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f09c5504-0c60-4503-9ce3-5690345b8ffc"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2025) });
        }
    }
}
