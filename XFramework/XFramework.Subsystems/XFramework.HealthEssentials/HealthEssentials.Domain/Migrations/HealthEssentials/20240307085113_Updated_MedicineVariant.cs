using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_MedicineVariant : Migration
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
                values: new object[] { new Guid("c1300316-3d01-4dcb-856d-649f20d78534"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3911) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fd82188c-52a3-4b04-a8cc-e30d2619d031"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3893) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7b6568f1-4acc-466c-a3fe-a95e0fec8dff"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3913) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b12fe4d7-430d-4498-9d2c-4a9b2705fbc1"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3884) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e1777f73-95f4-4cce-83ff-c20be233b73f"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3891) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ce77fc61-0632-4908-bd17-0679c83ecc9a"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3909) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7e39f254-985c-48ce-89ee-3aa54ff8278b"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3904) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f47e5ed0-7c26-47b0-8e27-eb9c6ccf2555"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3915) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2dc8b263-0a89-49f6-ad07-32c007a08482"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3902) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("558423d0-8a3f-44a1-af69-f06ad301f59c"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3889) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("427a110c-7242-4f4a-971c-6accc3b91ef2"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3896) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6358046b-6402-4ea9-b5f4-297c3cba3aca"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3898) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4d95a185-979e-47b0-86bd-4916b736cb7b"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3907) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1d0fc2b9-690c-46c7-bd72-84e90303a773"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3813) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("c9053f63-1bcd-484d-bd44-f8d408c4b8df"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3827) });
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
    }
}
