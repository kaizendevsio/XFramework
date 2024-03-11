using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_Medicine_Variant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjobordermedicine_medicine_id_fk",
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

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicineId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
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

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineVariantId",
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

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineVariantId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationJobOrderMedicine_Medicine_MedicineId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "consultationjobordermedicine_medicine_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineVariantId",
                principalSchema: "Medicine",
                principalTable: "MedicineVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationJobOrderMedicine_Medicine_MedicineId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropForeignKey(
                name: "consultationjobordermedicine_medicine_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineVariantId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "MedicineVariantId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

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
                name: "MedicineId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
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
                values: new object[] { new Guid("c55ddc0e-923d-4d89-b56c-a64fb9c22708"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9544) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("59eb04ff-0540-42be-af92-731d15a16ab1"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9489) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3e4ea6b7-5ac0-4d9d-8751-fec55eef33d6"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9553) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cd190746-58e2-42c9-a823-d6c599de64ff"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9456) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("218ee846-3157-4d0c-afd9-bd2428a80cb3"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9481) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("291d10ae-f7d8-4fbe-970c-03161a5fac16"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9533) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("094d41d2-7ff7-47a5-a0b6-c63b84ca6003"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9521) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8f997d9e-e67f-469c-a484-36b7f655dd7e"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9555) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ccbb5a1a-934a-43b2-bbf6-38eedd531a5d"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9514) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e119dc46-179b-4f91-81dd-09fa8b4f292e"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9468) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4a4cbf58-4914-4258-84cf-41b36656492b"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9495) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("dd1ef986-28d2-4df6-8c07-30d16db5bb19"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9504) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("77af754c-f855-466f-b153-769b587c2335"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9524) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e819bcd1-1960-408e-8462-2710c2c71f4a"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9330) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0023497c-b0b9-4bee-9fe3-133b852c1298"), new DateTime(2024, 3, 7, 5, 15, 29, 229, DateTimeKind.Utc).AddTicks(9346) });

            migrationBuilder.AddForeignKey(
                name: "consultationjobordermedicine_medicine_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
