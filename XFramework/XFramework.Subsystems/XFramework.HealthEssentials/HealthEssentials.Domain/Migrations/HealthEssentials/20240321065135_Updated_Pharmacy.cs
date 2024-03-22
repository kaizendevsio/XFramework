using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Updated_Pharmacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pharmacystocks_pharmacy_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks");

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

            migrationBuilder.AddForeignKey(
                name: "pharmacystocks_pharmacy_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "PharmacyId",
                principalSchema: "Pharmacy",
                principalTable: "PharmacyBranch",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pharmacystocks_pharmacy_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks");

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
                values: new object[] { new Guid("c1300316-3d01-4dcb-856d-649f20d78534"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3911) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("fd82188c-52a3-4b04-a8cc-e30d2619d031"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3893) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7b6568f1-4acc-466c-a3fe-a95e0fec8dff"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3913) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("b12fe4d7-430d-4498-9d2c-4a9b2705fbc1"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3884) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("e1777f73-95f4-4cce-83ff-c20be233b73f"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3891) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("ce77fc61-0632-4908-bd17-0679c83ecc9a"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3909) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("7e39f254-985c-48ce-89ee-3aa54ff8278b"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3904) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("f47e5ed0-7c26-47b0-8e27-eb9c6ccf2555"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3915) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("2dc8b263-0a89-49f6-ad07-32c007a08482"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3902) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("558423d0-8a3f-44a1-af69-f06ad301f59c"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3889) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("427a110c-7242-4f4a-971c-6accc3b91ef2"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3896) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("6358046b-6402-4ea9-b5f4-297c3cba3aca"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3898) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("4d95a185-979e-47b0-86bd-4916b736cb7b"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3907) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("1d0fc2b9-690c-46c7-bd72-84e90303a773"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3813) });

            migrationBuilder.UpdateData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                columns: new[] { "ConcurrencyStamp", "CreatedAt" },
                values: new object[] { new Guid("c9053f63-1bcd-484d-bd44-f8d408c4b8df"), new DateTime(2024, 3, 7, 8, 51, 11, 913, DateTimeKind.Utc).AddTicks(3827) });

            migrationBuilder.AddForeignKey(
                name: "pharmacystocks_pharmacy_id_fk",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "PharmacyId",
                principalSchema: "Pharmacy",
                principalTable: "Pharmacy",
                principalColumn: "ID");
        }
    }
}
