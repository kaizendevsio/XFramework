using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Update_LogisticJobOrder_ScheduleId_Nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "logisticjoborder_schedule_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder");

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
                name: "ScheduleId",
                schema: "Logistic",
                table: "LogisticJobOrder",
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
                values: new object[] { new Guid("b916e051-2d38-4019-bea5-1cca6bdef7f5"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8305) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("70653f44-72c9-4e51-bc08-e20b49237d18"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8287) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e9f3bee8-2e92-4b8f-9667-6ad5e45dc7d4"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8307) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a64eec35-fcd7-4aa5-aca3-dafdfd11cf4e"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8280) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ab13d943-b88b-4b83-9fbe-f23e8b0f19a1"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8285) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cd5af2bb-ee5c-4409-b148-197536b86a9a"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8303) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3063cece-09fe-4bbe-97af-88958ce7b865"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8299) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("23ba5556-92d5-456f-86b2-349103949ac5"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8309) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3ee7c74d-8a2b-4319-820a-8134c4da7309"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8297) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f2ebacfb-2ac3-4581-a44f-c4ba387788cb"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8282) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2b04157e-6bb6-4c51-b0c9-2516ab5f2467"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8290) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("dec70e34-1a57-4cf9-86b1-372251489722"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8292) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("263b37bc-287b-4011-b328-d5be8fd193ba"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8301) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8297a383-dea7-48c6-b684-18d3f5358e53"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8251) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("41c85346-df52-457d-9d8a-978f5a097e66"), new DateTime(2024, 4, 15, 0, 22, 10, 896, DateTimeKind.Utc).AddTicks(8256) });

            migrationBuilder.AddForeignKey(
                name: "logisticjoborder_schedule_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "ScheduleId",
                principalSchema: "Schedule",
                principalTable: "Schedule",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "logisticjoborder_schedule_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder");

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
                name: "ScheduleId",
                schema: "Logistic",
                table: "LogisticJobOrder",
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
                values: new object[] { new Guid("3118dc09-2f47-4a5b-a1f1-e6a6ed517761"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4496) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0dbb3e50-e12f-4fb3-956b-55aaef77f342"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4477) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f6451101-c308-48b9-a21e-ce0f0c5f957b"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4498) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ceea5ba7-cede-47d9-ad1e-41edc1ecc1c4"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4468) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("128c4b74-5490-4b1c-8109-60e7bfb9fcb1"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4474) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6ca664f5-9cd5-4218-8d58-3d97f7e34d97"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4494) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7568466a-89e2-4b8b-b030-ff553c79bfdc"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4489) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("38b0b5e1-771d-4c75-a7c1-35397f12004e"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4500) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8849b7bf-37de-44c1-b490-982b1f1fc544"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4486) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("90fa9e97-ea55-40d9-a6ab-a21b0027d2e4"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4470) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("64dbfa43-7229-49e6-8111-00c4588e3759"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4480) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cfad6826-9c46-4981-87f0-966246d9cef3"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4482) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b7e69ffa-6d0a-4826-acf6-c28d0f7bb4f1"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4491) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e71df358-3380-422d-963e-0d3f8d605d68"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4432) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a336a6fb-c7cd-47f2-9d65-68cc6ddff794"), new DateTime(2024, 4, 15, 0, 13, 49, 542, DateTimeKind.Utc).AddTicks(4436) });

            migrationBuilder.AddForeignKey(
                name: "logisticjoborder_schedule_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "ScheduleId",
                principalSchema: "Schedule",
                principalTable: "Schedule",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
