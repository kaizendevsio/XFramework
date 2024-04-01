using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Added_Pharmacy_Stock_Cost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "PharmacyBranchId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyStocks_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "IX_PharmacyStocks_PharmacyBranchId");

            migrationBuilder.AddColumn<decimal>(
                name: "CostEx",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostInc",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceEx",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceInc",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

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
                values: new object[] { new Guid("8c2d3e5b-ab01-48e0-915f-89868070dbca"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2115) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("69b7186d-dde2-4fdb-9c97-aa71091ccfe6"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2098) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0b0c18d7-eb7f-4343-8262-b03243f69986"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2118) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7207fb73-60ca-44ab-96c0-b5f469076873"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2052) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("d8779c0b-3d73-452c-8489-32554c5a2ef1"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2084) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2b9c6382-beb1-4b3e-85db-d44adb94c6e0"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2113) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e45e51cf-43db-4179-bf09-28b177b49da6"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2108) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("36b456f8-317d-4bc0-866c-a345ee76a822"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2121) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("00610f62-85f6-4728-a4fe-0fcfb4204d23"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2106) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bbaff277-29e1-45c2-a62d-ed1de9ec601c"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2082) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6ff3aeaa-9482-402d-94bc-4854ac350d19"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2101) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("771f8fb3-0445-4934-a8fb-1ccd9702a0fd"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2104) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0e2a047f-4158-4b78-97c8-819c7cf1caeb"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2110) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("80bb4321-b1f9-477c-a328-ab97edd1393a"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2021) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f09c5504-0c60-4503-9ce3-5690345b8ffc"), new DateTime(2024, 3, 22, 12, 55, 59, 420, DateTimeKind.Utc).AddTicks(2025) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostEx",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "CostInc",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "PriceEx",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "PriceInc",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.RenameColumn(
                name: "PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "PharmacyId");

            migrationBuilder.RenameIndex(
                name: "IX_PharmacyStocks_PharmacyBranchId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                newName: "IX_PharmacyStocks_PharmacyId");

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
                values: new object[] { new Guid("66c451bd-15e7-4049-9d59-0651cf83a9a5"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2390) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bf766429-b1b1-4637-b36e-b8f4a13608ca"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2372) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("40f0ac97-1218-4bf9-b10f-ce61648ba5de"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2393) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("54efdcb6-60ff-4b4e-ae40-560626407ad2"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2363) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("187b4965-348e-43a5-a234-5d60593287aa"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2370) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("90a22091-a269-40d0-b6f5-14aea1673975"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2388) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("cc164016-df4c-49ed-aa9a-987d69dc089b"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2384) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("c9e01585-0fac-4ba6-b238-13b06046cbb5"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2395) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7361cceb-30c9-4887-83b6-dd4341a9cc72"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2382) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4a7e0342-3f65-4280-a982-f4122fe17594"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2366) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("9cff751a-cf78-4395-97bb-35f58c60c1a4"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2375) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("0d7e78a5-c885-4ab3-a443-03c08fc4a6ed"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2378) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("18fc3da5-cc49-4806-a055-534461086f2f"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2386) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7891eeba-a1b6-48f0-8636-f2c840544b39"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2313) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("bc7331be-c67d-4e14-be54-e925e777934c"), new DateTime(2024, 3, 21, 6, 51, 34, 749, DateTimeKind.Utc).AddTicks(2321) });
        }
    }
}
