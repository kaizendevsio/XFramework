using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthEssentials.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class AddedSeedData : Migration
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

            migrationBuilder.InsertData(
                schema: "Patient",
                table: "PatientTypeGroup",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), new Guid("9a60f533-cc8c-41b5-8862-f7c7c6bc083f"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8270), null, false, true, "General", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), new Guid("642fd6c5-4dd2-42c0-9c3b-d7f38c0895b8"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8275), null, false, true, "Specialized", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Patient",
                table: "PatientType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "Description", "GroupId", "IsDeleted", "IsEnabled", "Name", "SortOrder", "TenantId" },
                values: new object[,]
                {
                    { new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"), new Guid("33c435fb-9f6d-4bd9-9cc5-8d07dc228147"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8386), null, "Relief from the symptoms and stress of a serious illness", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Palliative Care", 6, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"), new Guid("6dc1b0f7-fd01-43e7-83f8-1d9002c3d279"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8319), null, "Immediate medical attention for life-threatening conditions", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Emergency", 3, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"), new Guid("14b54c01-e132-4df9-bbcb-9c2b0b0895af"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8389), null, "Patients visiting for outpatient services without overnight stay", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Ambulatory", 7, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"), new Guid("185f1d21-0fa6-4d4c-a256-b75818ed99c1"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8312), null, "Outpatient services", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Outpatient", 1, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("486917df-2b21-41fe-aa03-c564014f8cad"), new Guid("35000149-391a-4905-9f12-c9e32382e84f"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8317), null, "Specialized pediatric services", new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), false, true, "Pediatric", 1, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"), new Guid("d6d602d9-1885-4009-a108-9ccd5871f439"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8384), null, "Treatment for mental health conditions", new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), false, true, "Psychiatric", 5, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"), new Guid("e06e97bc-8ef7-4465-8db7-44e073d9d685"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8370), null, "Care for childbirth and postnatal services", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Maternity", 5, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"), new Guid("4448305c-b52d-4f7a-8f2f-bfdbfdc6fb58"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8393), null, "Medical care or treatment provided at the patient's home", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Home Care", 8, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"), new Guid("9838e837-d241-43be-8af7-eaf0bc60ce16"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8367), null, "Recovery and rehabilitation services for post-surgery or injury", new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), false, true, "Rehabilitation", 3, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("da22eb54-e064-43d1-89ed-51591f21f903"), new Guid("7ea421c7-fd01-4c2c-a581-0b2dda98dc84"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8315), null, "Inpatient services", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Inpatient", 2, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"), new Guid("4036fb4d-e181-43da-b8ad-1b5d1503658a"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8328), null, "Patients admitted for surgical procedures", new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), false, true, "Surgical", 2, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e36b4212-3add-452f-8448-825242821176"), new Guid("5691d972-11f1-4056-ae79-aefa98918b57"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8336), null, "Long-term care for ongoing conditions like diabetes, heart disease", new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), false, true, "Chronic Care", 4, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"), new Guid("0c5d0a19-fd71-4aa6-9bc2-a50b9d9cb6f2"), new DateTime(2024, 1, 18, 11, 30, 11, 498, DateTimeKind.Utc).AddTicks(8382), null, "Specialized care for elderly patients", new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), false, true, "Geriatric", 4, new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("486917df-2b21-41fe-aa03-c564014f8cad"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("da22eb54-e064-43d1-89ed-51591f21f903"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("e36b4212-3add-452f-8448-825242821176"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientType",
                keyColumn: "ID",
                keyValue: new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"));

            migrationBuilder.DeleteData(
                schema: "Patient",
                table: "PatientTypeGroup",
                keyColumn: "ID",
                keyValue: new Guid("7a284dea-f6c1-4025-9149-842ccae76236"));

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
        }
    }
}
