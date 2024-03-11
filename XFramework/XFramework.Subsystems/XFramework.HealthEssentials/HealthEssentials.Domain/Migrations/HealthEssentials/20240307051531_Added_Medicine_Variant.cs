using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Added_Medicine_Variant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pharmacyjobordermedicine_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropForeignKey(
                name: "pharmacystocks_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicineId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicineId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

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

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UnavailabilityHandling",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UnavailabilityHandling",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag",
                type: "uuid",
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

            migrationBuilder.CreateTable(
                name: "MedicineVariant",
                schema: "Medicine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Dosage = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineVariant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineVariant_Medicine_MedicineId",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicineVariant_Unit_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStocks_MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "MedicineVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineTag_MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag",
                column: "MedicineVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineVariant_MedicineId",
                schema: "Medicine",
                table: "MedicineVariant",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineVariant_UnitId",
                schema: "Medicine",
                table: "MedicineVariant",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicineTag_MedicineVariant_MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag",
                column: "MedicineVariantId",
                principalSchema: "Medicine",
                principalTable: "MedicineVariant",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PharmacyJobOrderMedicine_Medicine_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "pharmacyjobordermedicine_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineVariantId",
                principalSchema: "Medicine",
                principalTable: "MedicineVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PharmacyStocks_Medicine_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "pharmacystocks_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "MedicineVariantId",
                principalSchema: "Medicine",
                principalTable: "MedicineVariant",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicineTag_MedicineVariant_MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag");

            migrationBuilder.DropForeignKey(
                name: "FK_PharmacyJobOrderMedicine_Medicine_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropForeignKey(
                name: "pharmacyjobordermedicine_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropForeignKey(
                name: "FK_PharmacyStocks_Medicine_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropForeignKey(
                name: "pharmacystocks_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropTable(
                name: "MedicineVariant",
                schema: "Medicine");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyStocks_MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropIndex(
                name: "IX_MedicineTag_MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag");

            migrationBuilder.DropColumn(
                name: "MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "MedicineVariantId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "UnavailabilityHandling",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "UnavailabilityHandling",
                schema: "Pharmacy",
                table: "PharmacyJobOrder");

            migrationBuilder.DropColumn(
                name: "MedicineVariantId",
                schema: "Medicine",
                table: "MedicineTag");

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicineId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MedicineId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "pharmacyjobordermedicine_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "pharmacystocks_medicine_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "MedicineId",
                principalSchema: "Medicine",
                principalTable: "Medicine",
                principalColumn: "ID");
        }
    }
}
