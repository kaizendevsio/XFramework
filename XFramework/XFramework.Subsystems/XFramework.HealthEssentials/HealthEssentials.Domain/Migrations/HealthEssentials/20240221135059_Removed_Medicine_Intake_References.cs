using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class RemovedMedicineIntakeReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjobordermedicine_medicineintake_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropForeignKey(
                name: "pharmacyjobordermedicine_medicineintake_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineIntakeId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineIntakeId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "MedicineIntakeId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "MedicineIntakeId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

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
                values: new object[] { new Guid("1b3925f8-588b-41a5-948a-1f46c09f1f85"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3403) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0cb03850-15f5-47d4-b3aa-0d8f8555fce0"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3377) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("50d68217-a17a-486b-9baa-a536591418a2"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3406) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("45d52bd6-4081-4b23-8a8e-d2b693465f8b"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3369) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("75ffd9ce-349a-47e2-bd5f-4f4d119c5124"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3374) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d5b50d38-6f34-4638-9d3b-ccb73a77bd92"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3401) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9e3978b9-d0f6-4560-ad23-e1d01912c11c"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3394) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1d6c0504-44ba-4f13-a5cf-c85029cbc089"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3410) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2ac11649-9e98-4775-8264-10917a980c9e"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3390) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b89ec2ab-e1ed-4137-9f2f-ed15cbd9b172"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3372) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("54dfc5da-9805-46d4-9fa8-fc6caade1377"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3385) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d76ec6c7-4144-4547-9653-104eb7273b65"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3388) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("27d71827-1fee-4ed0-83bf-047db9d483d2"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3396) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4dc66f4c-4928-479c-9f0d-8c5ccf0dc674"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3319) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("dc183a21-d5d7-46df-b2ad-cca3241b78b8"), new DateTime(2024, 2, 21, 13, 50, 58, 350, DateTimeKind.Utc).AddTicks(3325) });
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

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineIntakeId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineIntakeId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineIntakeId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineIntakeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineIntakeId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineIntakeId");

            migrationBuilder.AddForeignKey(
                name: "consultationjobordermedicine_medicineintake_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineIntakeId",
                principalSchema: "Medicine",
                principalTable: "MedicineIntake",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "pharmacyjobordermedicine_medicineintake_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineIntakeId",
                principalSchema: "Medicine",
                principalTable: "MedicineIntake",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
