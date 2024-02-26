using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class RenamedLocationsToBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

            migrationBuilder.DropForeignKey(
                name: "laboratoryjoborder_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryJobOrder");

            migrationBuilder.DropForeignKey(
                name: "laboratorymember_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryMember");

            migrationBuilder.DropForeignKey(
                name: "laboratoryservice_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryService");

            migrationBuilder.DropTable(
                name: "LaboratoryLocationTag",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryLocation",
                schema: "Laboratory");

            migrationBuilder.RenameColumn(
                name: "PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyService",
                newName: "PharmacyBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyService_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyService",
                newName: "IX_PharmacyService_PharmacyBranchId");

            migrationBuilder.RenameColumn(
                name: "PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                newName: "PharmacyBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyMember_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                newName: "IX_PharmacyMember_PharmacyBranchId");

            migrationBuilder.RenameColumn(
                name: "PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                newName: "PharmacyBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyJobOrder_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                newName: "IX_PharmacyJobOrder_PharmacyBranchId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryService",
                newName: "LaboratoryBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryService_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryService",
                newName: "IX_LaboratoryService_LaboratoryBranchId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                newName: "LaboratoryBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryMember_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                newName: "IX_LaboratoryMember_LaboratoryBranchId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                newName: "LaboratoryBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryJobOrder_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                newName: "IX_LaboratoryJobOrder_LaboratoryBranchId");

            migrationBuilder.RenameColumn(
                name: "SuggestedLaboratoryLocationId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                newName: "SuggestedLaboratoryBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_ConsultationJobOrderLaboratory_SuggestedLaboratoryLocationId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                newName: "IX_ConsultationJobOrderLaboratory_SuggestedLaboratoryBranchId");

            migrationBuilder.AddColumn<string>(
                name: "Device",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastActivityType",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineSince",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusMessage",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "text",
                nullable: true);

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

            migrationBuilder.CreateTable(
                name: "LaboratoryBranch",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdentityCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    AlternativePhone = table.Column<string>(type: "character varying", nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnlineSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusMessage = table.Column<string>(type: "text", nullable: true),
                    LastActivityType = table.Column<string>(type: "text", nullable: true),
                    Device = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryBranch_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryBranch_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryBranchTag",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    LaboratoryBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryBranchtag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryBranchtag_laboratoryBranch_id_fk",
                        column: x => x.LaboratoryBranchId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryBranch",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratorylocationtag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryBranch_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryBranch",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryBranchTag_LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryBranchTag",
                column: "LaboratoryBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryBranchTag_TagId",
                schema: "Laboratory",
                table: "LaboratoryBranchTag",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "consultationjoborderlaboratory_laboratoryBranch_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "SuggestedLaboratoryBranchId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryBranch",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "laboratoryjoborder_laboratoryBranch_id_fk",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "LaboratoryBranchId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryBranch",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "laboratorymember_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryMember",
                column: "LaboratoryBranchId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryBranch",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "laboratoryservice_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "LaboratoryBranchId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryBranch",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjoborderlaboratory_laboratoryBranch_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

            migrationBuilder.DropForeignKey(
                name: "laboratoryjoborder_laboratoryBranch_id_fk",
                schema: "Laboratory",
                table: "LaboratoryJobOrder");

            migrationBuilder.DropForeignKey(
                name: "laboratorymember_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryMember");

            migrationBuilder.DropForeignKey(
                name: "laboratoryservice_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryService");

            migrationBuilder.DropTable(
                name: "LaboratoryBranchTag",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryBranch",
                schema: "Laboratory");

            migrationBuilder.DropColumn(
                name: "Device",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "LastActivityType",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "OnlineSince",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "StatusMessage",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.RenameColumn(
                name: "PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyService",
                newName: "PharmacyLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyService_PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyService",
                newName: "IX_PharmacyService_PharmacyLocationId");

            migrationBuilder.RenameColumn(
                name: "PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                newName: "PharmacyLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyMember_PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                newName: "IX_PharmacyMember_PharmacyLocationId");

            migrationBuilder.RenameColumn(
                name: "PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                newName: "PharmacyLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyJobOrder_PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                newName: "IX_PharmacyJobOrder_PharmacyLocationId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryService",
                newName: "LaboratoryLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryService_LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryService",
                newName: "IX_LaboratoryService_LaboratoryLocationId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                newName: "LaboratoryLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryMember_LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                newName: "IX_LaboratoryMember_LaboratoryLocationId");

            migrationBuilder.RenameColumn(
                name: "LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                newName: "LaboratoryLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_LaboratoryJobOrder_LaboratoryBranchId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                newName: "IX_LaboratoryJobOrder_LaboratoryLocationId");

            migrationBuilder.RenameColumn(
                name: "SuggestedLaboratoryBranchId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                newName: "SuggestedLaboratoryLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_ConsultationJobOrderLaboratory_SuggestedLaboratoryBranchId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                newName: "IX_ConsultationJobOrderLaboratory_SuggestedLaboratoryLocationId");

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

            migrationBuilder.CreateTable(
                name: "LaboratoryLocation",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlternativePhone = table.Column<string>(type: "character varying", nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    IdentityCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratorylocation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorylocation_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryLocationTag",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    LaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratorylocationtag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorylocationtag_laboratorylocation_id_fk",
                        column: x => x.LaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratorylocationtag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("71b6b121-65f4-4c3a-be4a-0355e281ad14"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(677) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a8e9bf33-8c57-42a9-8933-4424afddc1c8"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(658) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("c7cedb11-b68d-4f39-b9a4-82dbc469e3ee"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(680) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("41945689-8211-4757-8fd7-7fcf8c038cbe"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(650) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2df0c9fb-3e1b-435d-8dd5-eb6b22d20c1f"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(655) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9385f452-4bfd-4c64-90a5-2dc9b3458ea7"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(675) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("eb08561e-e49c-4057-9e7d-9cfc6ff27f34"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(668) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4ae8c49a-428f-4e93-a5e6-9eb83332bb25"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(682) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("a86eb66f-7271-4ae4-ad92-ae734cb850f0"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(665) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fd91f2f8-fab8-4aa9-a254-942de1d59259"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(653) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9f50eec1-2f37-429f-aca8-4a204bbb05f7"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(660) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9610637e-9e77-4cbd-b69c-166836149a0d"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(663) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2c4b4388-2338-4612-a44b-4adf36d6252a"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(672) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("41a0aa59-4e4d-43ba-b90e-82580dfb7541"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(594) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4fe8208e-3bd0-4245-bb8c-c397b021ded4"), new DateTime(2024, 2, 21, 16, 55, 13, 756, DateTimeKind.Utc).AddTicks(599) });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocation_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocationTag_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                column: "LaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocationTag_TagId",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "SuggestedLaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "laboratoryjoborder_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "LaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "laboratorymember_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryMember",
                column: "LaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "laboratoryservice_laboratorylocation_id_fk",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "LaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID");
        }
    }
}
