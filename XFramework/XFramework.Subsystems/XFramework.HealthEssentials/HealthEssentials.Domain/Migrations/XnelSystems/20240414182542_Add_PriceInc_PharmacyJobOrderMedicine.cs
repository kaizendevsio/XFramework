using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Add_PriceInc_PharmacyJobOrderMedicine : Migration
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

            migrationBuilder.AddColumn<decimal>(
                name: "PriceEx",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceInc",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceEx",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "PriceInc",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

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
                values: new object[] { new Guid("c45f1129-5ad9-4f45-82d2-46d0748e2548"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8432) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d16781ff-e7a8-4910-a5e9-ccbb99d9fe27"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8416) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("94ef1c6a-d24b-46ce-8e07-3c5fde721d57"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8434) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e3c990f6-fd96-42a3-9c4b-071533403cce"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8408) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("36d67760-5807-4b81-ab7a-719c35e80d3a"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8414) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6541c661-9324-4a03-9ae5-7f0ef70d80ed"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8430) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0cf5293c-739f-4d18-98c9-cf38de4b7ef2"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8426) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bd377c0b-46dd-4d0f-96d0-8ddbe38bc6fb"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8439) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("273a9bb5-4fdd-43be-adc8-f58e72584b19"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8424) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cf0b7dbb-e32b-47d4-8192-630eb8b34545"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8412) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ca140d75-9192-41ed-96bc-96cea213c37a"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8418) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("88cb05ea-b256-46d7-8304-693e14755d8c"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8421) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("73e39281-4357-4866-8e00-aac529de0098"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8428) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e03deed5-9d75-4541-8ab8-e39d036e605e"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8353) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("8d5f7595-dab3-4d42-8787-0a9e2928992a"), new DateTime(2024, 4, 3, 16, 26, 10, 203, DateTimeKind.Utc).AddTicks(8359) });
        }
    }
}
