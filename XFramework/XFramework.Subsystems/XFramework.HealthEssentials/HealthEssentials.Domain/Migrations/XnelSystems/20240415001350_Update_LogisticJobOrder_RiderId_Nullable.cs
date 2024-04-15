using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Update_LogisticJobOrder_RiderId_Nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "logisticjoborder_logisticrider_id_fk",
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
                name: "RiderId",
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
                name: "logisticjoborder_logisticrider_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "RiderId",
                principalSchema: "Logistic",
                principalTable: "LogisticRider",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "logisticjoborder_logisticrider_id_fk",
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
                name: "RiderId",
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
                values: new object[] { new Guid("79c15315-504f-4bc8-b3d8-79beb18574f6"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5321) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d5dee4ed-12a9-447c-8c2a-0dd885a62af1"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5285) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("5274c3f0-da40-4046-aaae-f12ca1d1a2e8"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5323) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d2deee0f-43bd-4e0e-8abc-7f347e170ea6"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5260) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("aa952982-23a3-474b-9f31-7e9ec94c845d"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5283) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b9557bb7-534e-48a1-8b4c-b5a642cda5b3"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5317) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2ef3547d-9e5c-44a8-ab65-9c3ba0cbc276"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5313) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6879e78a-da19-4b96-a21f-f5b9a29bda3c"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5325) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("84987cbd-68b8-4dce-a1b5-c97bdf9ece23"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5310) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("97f76399-8c5c-4605-a070-7a2fb6554c70"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5263) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0c7d0c36-5bf4-4427-a020-9188986c463f"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5292) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8e10aff6-9bd9-4a7d-84a1-32b40fd747e7"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5297) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e7bd95f5-8d58-443e-a35a-311d9017e149"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5315) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("68964642-3944-456f-9495-2ed528a1b7b7"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5195) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f5604db1-dbed-4136-ad98-28d29e6fa3d4"), new DateTime(2024, 4, 14, 23, 38, 13, 845, DateTimeKind.Utc).AddTicks(5203) });

            migrationBuilder.AddForeignKey(
                name: "logisticjoborder_logisticrider_id_fk",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "RiderId",
                principalSchema: "Logistic",
                principalTable: "LogisticRider",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
