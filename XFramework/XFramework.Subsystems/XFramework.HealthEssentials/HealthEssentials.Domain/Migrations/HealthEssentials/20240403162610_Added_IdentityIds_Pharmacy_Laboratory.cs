using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class Added_IdentityIds_Pharmacy_Laboratory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NearestExpiryDate",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "timestamp with time zone",
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

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityId",
                schema: "Pharmacy",
                table: "PharmacyBranch",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityId",
                schema: "Pharmacy",
                table: "Pharmacy",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityId",
                schema: "Laboratory",
                table: "LaboratoryBranch",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityId",
                schema: "Laboratory",
                table: "Laboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NearestExpiryDate",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "IdentityId",
                schema: "Pharmacy",
                table: "PharmacyBranch");

            migrationBuilder.DropColumn(
                name: "IdentityId",
                schema: "Pharmacy",
                table: "Pharmacy");

            migrationBuilder.DropColumn(
                name: "IdentityId",
                schema: "Laboratory",
                table: "LaboratoryBranch");

            migrationBuilder.DropColumn(
                name: "IdentityId",
                schema: "Laboratory",
                table: "Laboratory");

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
        }
    }
}
