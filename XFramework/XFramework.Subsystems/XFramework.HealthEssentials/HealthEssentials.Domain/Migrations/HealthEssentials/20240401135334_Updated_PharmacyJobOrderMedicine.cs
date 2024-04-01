using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_PharmacyJobOrderMedicine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pharmacyjobordermedicine_unit_id_fk_3",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.AlterColumn<Guid>(
                name: "IntakeUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "DurationUnitId",
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

            migrationBuilder.AlterColumn<Guid>(
                name: "DosageUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
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
                values: new object[] { new Guid("8d61815b-acb8-4a38-a4a3-58edf7344b40"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3459) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b14c4959-e238-4552-a0b3-d780e9ce8927"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3413) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("946b0f6c-ccd3-4d58-998a-f014f37ffb93"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3461) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("64ee72fe-5488-4612-a1f3-8bab7e65111f"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3404) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("703bf7bd-9ea5-48b9-88ee-8f9c0a2ef680"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3410) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2f70e499-6574-423c-a697-ec4466743a9f"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3457) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("173ad403-04cd-45bb-b831-7d80c494a6e1"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3428) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ee82f745-9349-4d84-b663-7d4a20006ed8"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3467) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9f3b0ef1-befb-41f9-85a7-80aa47add383"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3426) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("87b1677a-d22f-450c-b028-38cb4154e197"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3407) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("97ea523b-5146-4133-aa7a-95c49f76bf95"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3421) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bf157afc-bcfc-44a2-b8b2-9f6e9c95eb7a"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3424) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e28488ed-424b-4b32-a95b-39c0401a0f42"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3455) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("47f29dfe-001b-4517-a640-bdb1cf995119"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3363) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b70931ae-1b45-4ac2-951f-ca9b6dee1602"), new DateTime(2024, 4, 1, 13, 53, 33, 190, DateTimeKind.Utc).AddTicks(3368) });

            migrationBuilder.AddForeignKey(
                name: "pharmacyjobordermedicine_unit_id_fk_3",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "DosageUnitId",
                principalSchema: "Unit",
                principalTable: "Unit",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pharmacyjobordermedicine_unit_id_fk_3",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.AlterColumn<Guid>(
                name: "IntakeUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DurationUnitId",
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

            migrationBuilder.AlterColumn<Guid>(
                name: "DosageUnitId",
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
                values: new object[] { new Guid("643b6c22-17c5-4f92-b475-5f78467376a9"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7982) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("30556a97-a31d-477d-8678-5898f249a2e0"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7964) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b1dbac3e-7a5e-4010-930b-6929150eba20"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7984) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("126d97ae-4077-4501-99a9-8d514133103d"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7946) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6796cf12-9d81-4fd9-911b-c440c63b130f"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7962) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("18118902-55d4-4851-92e0-c0aee1534567"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7979) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("26b5e5cc-0e80-4d19-a7b5-12e1635ff4e5"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7973) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4104e387-2d07-4291-bbe4-79199a1cf6b4"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7986) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4e1dd6d8-d42c-4a16-9050-a71f6f1909f8"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7971) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("499797b5-23c7-4a9a-b136-99c98398b519"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7960) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("571e0800-06cf-4a2f-9579-92b900a36cd5"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7967) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("df634e0f-4926-4730-b58d-6e13bf340780"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7969) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ea8bc18b-7629-47bd-89d7-b1da1a5a3cca"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7976) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3703ab1c-48d2-4b12-a95e-06590375b78c"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7913) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f072bc45-d395-4cc9-af89-495b14b4aa68"), new DateTime(2024, 3, 31, 7, 4, 32, 16, DateTimeKind.Utc).AddTicks(7917) });

            migrationBuilder.AddForeignKey(
                name: "pharmacyjobordermedicine_unit_id_fk_3",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "DosageUnitId",
                principalSchema: "Unit",
                principalTable: "Unit",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
