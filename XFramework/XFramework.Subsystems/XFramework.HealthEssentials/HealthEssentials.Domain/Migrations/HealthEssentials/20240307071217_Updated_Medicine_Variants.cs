using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_Medicine_Variants : Migration
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
                values: new object[] { new Guid("704263c1-72bb-4e72-af9a-c693d1f06f07"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7507) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("91b4bffa-8f6d-4e35-8e5c-1c55fd2863d1"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7489) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0f03a420-b873-439c-b2db-8ad15df09158"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7509) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("71f517ad-dc25-473c-bcb7-2474a4486aeb"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7463) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3a051dbd-57ce-45a2-b26d-6fe639347ce9"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7487) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3ba01259-5c90-47a9-a6db-8b3790f4001e"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7505) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8124b61c-b559-4503-bdec-4dd1d04a60e6"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7499) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0a2adc2b-5e7a-4a48-9dbe-fe4152f8dd1b"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7511) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7cb443ac-8886-4430-8d3f-9f304d0043bf"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7497) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("047118bc-d8a3-43f5-bc6d-e5ee822fcfbd"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7484) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("00841b65-0d83-4607-903a-4bfa982d75be"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7492) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e9269410-d522-4a9a-b34b-f6fd28a728af"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7494) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("682eb191-9c8c-4eb8-ab77-adff2e2479a5"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7501) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4465c4f3-4b38-4036-9f9c-2d353d19bb50"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7420) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cd3631c3-def6-4298-a0c2-809500f36455"), new DateTime(2024, 3, 7, 7, 12, 16, 746, DateTimeKind.Utc).AddTicks(7425) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                values: new object[] { new Guid("15045500-3180-4ec7-b76a-593776c85411"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(773) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8fac5874-c45b-4765-afc4-1bf7f8512af2"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(749) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fbb873f4-3b33-4cb1-aa55-7d59b1439e18"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(775) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7fcc7e9c-d7cd-42e3-9ecd-eaa48d5d9719"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(698) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a90efed8-bf53-409b-a4ac-2838a5ec3da3"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(746) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f76111b4-1e79-4b7e-92f3-b572287e3a7e"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(770) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("033309d8-ce76-4aaa-8498-6f62960c6be9"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(761) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d62bf35a-f59a-4d1c-81d4-c2c77abd4456"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(777) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7a2ace07-c186-43c0-86a9-af61a63bc572"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(758) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7d89e775-6ad0-4fbf-b881-989fe1e98d60"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(703) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("19fe78b7-e589-41be-9582-6946c0bdaaec"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(752) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("71d7cd0f-d113-4e30-a84c-9c9126995d58"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(754) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("acc6e83b-b893-465d-adb4-c470f08aabf1"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(763) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("95ee233d-1032-4057-9b1e-d96bd03fc089"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(666) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4d148cb5-d566-4eaf-8f6a-67c4ec7bfdf4"), new DateTime(2024, 3, 7, 5, 20, 16, 101, DateTimeKind.Utc).AddTicks(670) });
        }
    }
}
