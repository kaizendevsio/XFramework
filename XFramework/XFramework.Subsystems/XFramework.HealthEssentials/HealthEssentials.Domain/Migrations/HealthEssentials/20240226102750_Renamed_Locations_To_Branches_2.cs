using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class RenamedLocationsToBranches2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PharmacyLocation",
                schema: "Pharmacy",
                newName: "PharmacyBranch",
                newSchema: "Pharmacy");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyLocation_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyBranch",
                newName: "IX_PharmacyBranch_PharmacyId");

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
                values: new object[] { new Guid("75b3e544-dcaf-4047-b160-2ae26b3b1db6"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6725) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4f4e0cd7-d40f-4058-8dea-68e7388a25eb"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6704) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6547fe34-36a1-4e09-bca4-c4d93fe351d9"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6729) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3e55f3b1-cb24-4d35-9e3e-739f62e9af8e"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6679) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d5f2375c-9d39-418c-8b5f-b461e3ffa50a"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6684) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fa38dbe4-f7bd-4a8e-8cb0-d57700853f19"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6722) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2bc5de57-55cc-4460-acac-54a0e384d2e1"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6715) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("c922adb4-9ed0-4769-82bf-85ce470a31e7"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6735) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("44edad43-3113-49cb-9f40-988e9a7db6c4"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6712) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("05c510a1-857d-42e7-a662-bd27219ff37c"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6682) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7dd20f75-e2a0-4e64-94ad-a3ac12c1a0ba"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6708) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2a22c232-503d-4855-8537-4aaf021a5a85"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6710) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("caf03170-cde8-44c3-aebc-b3de057c04fe"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6718) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1c3bfd8c-ec10-4cfa-a30e-20b94a726685"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6640) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b13bbf4c-4ab2-49e1-b5bd-be48a58d30d1"), new DateTime(2024, 2, 26, 10, 27, 49, 62, DateTimeKind.Utc).AddTicks(6644) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PharmacyBranch",
                schema: "Pharmacy",
                newName: "PharmacyLocation",
                newSchema: "Pharmacy");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyBranch_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                newName: "IX_PharmacyLocation_PharmacyId");

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
                values: new object[] { new Guid("fef1ca7e-e655-421d-8a41-c5c72d13c55f"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2173) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8e6b7598-9010-4bbc-9a94-292488210139"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2132) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("252b8517-b50a-4b6b-93f1-ebb045ffd264"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2179) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6373d4db-4e60-4237-b786-2c9c2b1d109b"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2114) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0ee41351-7165-4a2c-aa45-061e58b113f7"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2126) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7d295ed8-090d-4d2d-a216-430de4335260"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2167) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("82abd18c-79b7-4e8a-a7f7-581498f5d05a"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2155) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5a20c213-b7aa-4182-b37c-d5d17fea9b51"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2186) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3ca286d4-f726-4e4b-b6b7-6334e22e0e23"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2149) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e130023d-e360-4f45-b51e-c7377b7dc25d"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2121) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5192b1ca-d9cf-49a5-bd34-99238b536d88"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2138) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("72ea518d-bcd4-4d34-9aca-fceb63b1ddfc"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2143) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5a30c21e-61c9-4d20-889e-2583810473b9"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2161) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("19659c08-784d-4025-a52c-e0e7af2020c1"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2042) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1da2875e-2948-4469-949d-6773bc3b14a0"), new DateTime(2024, 2, 26, 9, 32, 47, 645, DateTimeKind.Utc).AddTicks(2050) });
        }
    }
}
