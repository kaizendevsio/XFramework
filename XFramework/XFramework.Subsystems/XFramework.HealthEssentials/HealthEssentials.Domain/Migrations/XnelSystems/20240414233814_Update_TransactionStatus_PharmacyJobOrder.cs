using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Update_TransactionStatus_PharmacyJobOrder : Migration
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

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "integer",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

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

            migrationBuilder.AlterColumn<short>(
                name: "Status",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
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
                values: new object[] { new Guid("3e49ca7b-d9b6-472c-b58b-2b4917206666"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8757) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f4963f5d-4daf-49ce-a5c9-253f6ba684e6"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8702) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("508416f9-1477-4c8a-b7f7-2f74176ff411"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8759) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1bf24ca9-0ca2-4490-ad43-5fb361d380f3"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8695) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8ad83033-bf22-40c8-8c5b-07d717cc700d"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8700) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e0edd6d4-1cb6-43e8-8b6b-2a23da80a001"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8755) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("09c584c4-0cd9-4171-9320-ad2623af4135"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8728) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f96158c6-24ea-424f-9b01-e8d6aef6cdce"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8763) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("3517de6d-651b-413f-b9e9-e8e7932e0f5c"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8726) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cd459358-3e24-4826-8b1b-011c784ea49a"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8697) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ada36a4d-93c2-45fd-b04b-8fb705bacd64"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8720) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("851d3624-93c8-42ab-bc24-af3f3702affa"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8723) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e9fef9a9-d0f3-40d7-90a7-709b0efb0f50"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8733) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("550402f6-4a09-4328-a2cd-e77295b274a7"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8662) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e4591686-4e0d-4c64-a149-d7af7fe414a0"), new DateTime(2024, 4, 14, 18, 25, 42, 376, DateTimeKind.Utc).AddTicks(8668) });
        }
    }
}
