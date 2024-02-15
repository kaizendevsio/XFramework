using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class AddedIHasOnlineStatusproperties : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "Device",
                schema: "Doctor",
                table: "Doctor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                schema: "Doctor",
                table: "Doctor",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastActivityType",
                schema: "Doctor",
                table: "Doctor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Doctor",
                table: "Doctor",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "Doctor",
                table: "Doctor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineSince",
                schema: "Doctor",
                table: "Doctor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusMessage",
                schema: "Doctor",
                table: "Doctor",
                type: "text",
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
                values: new object[] { new Guid("6c2f351b-06e3-41a7-8398-64afaf960198"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1183) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("da781d0e-81e3-4116-aaf6-ff1ac8ee4969"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1144) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4eb542c3-cf05-4999-b09d-de8b4a2c7919"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1186) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6f6bad8f-0afb-4138-9ed0-49134cb87439"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1136) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8f5feb09-befe-4f0e-963f-951d5e8d4684"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1141) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b5684863-8c91-496a-950a-274191c6a20d"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1181) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d35315d8-d412-48c4-b399-30d8732433a1"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1176) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fa998ccf-8c92-4410-a4d6-c0910eb5467a"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1189) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e4c4291f-88a4-4c5f-a633-a8edb5d661db"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1174) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a6aab619-77d1-405e-ab4c-2e948a977b60"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1138) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8a7f1be6-8113-4b15-b59a-d1faec959053"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1167) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("251d4784-13c2-4a0f-88b5-e5cce5717ce1"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1170) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2afac3c1-fe85-4c01-b102-6120738a06a6"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1179) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cc5d9531-dcee-40d8-8715-48e7b1c3cb21"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1099) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1689bd23-a08d-49ec-b2e9-149744c79329"), new DateTime(2024, 2, 12, 12, 33, 19, 846, DateTimeKind.Utc).AddTicks(1103) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Device",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "LastActivityType",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "OnlineSince",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "StatusMessage",
                schema: "Doctor",
                table: "Doctor");

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
                values: new object[] { new Guid("33c435fb-9f6d-4bd9-9cc5-8d07dc228147"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8386) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6dc1b0f7-fd01-43e7-83f8-1d9002c3d279"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8319) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("14b54c01-e132-4df9-bbcb-9c2b0b0895af"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8389) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("185f1d21-0fa6-4d4c-a256-b75818ed99c1"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8312) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("35000149-391a-4905-9f12-c9e32382e84f"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8317) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d6d602d9-1885-4009-a108-9ccd5871f439"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8384) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e06e97bc-8ef7-4465-8db7-44e073d9d685"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8370) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4448305c-b52d-4f7a-8f2f-bfdbfdc6fb58"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8393) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9838e837-d241-43be-8af7-eaf0bc60ce16"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8367) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7ea421c7-fd01-4c2c-a581-0b2dda98dc84"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8315) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4036fb4d-e181-43da-b8ad-1b5d1503658a"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8328) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5691d972-11f1-4056-ae79-aefa98918b57"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8336) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0c5d0a19-fd71-4aa6-9bc2-a50b9d9cb6f2"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8382) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9a60f533-cc8c-41b5-8862-f7c7c6bc083f"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8270) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("642fd6c5-4dd2-42c0-9c3b-d7f38c0895b8"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8275) });
        }
    }
}
