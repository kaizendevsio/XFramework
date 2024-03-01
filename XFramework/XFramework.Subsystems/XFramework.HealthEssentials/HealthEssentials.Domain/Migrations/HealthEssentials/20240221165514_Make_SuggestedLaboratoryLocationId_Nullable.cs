using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class MakeSuggestedLaboratoryLocationIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "SuggestedLaboratoryLocationId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

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

            migrationBuilder.AddForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "SuggestedLaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "SuggestedLaboratoryLocationId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "SuggestedLaboratoryLocationId",
                principalSchema: "Laboratory",
                principalTable: "LaboratoryLocation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
