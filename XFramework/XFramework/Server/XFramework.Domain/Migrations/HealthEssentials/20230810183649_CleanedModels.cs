using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.HealthEssentials
{
    /// <inheritdoc />
    public partial class CleanedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "VendorTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "VendorTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Services",
                table: "VendorTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "VendorType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "VendorType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Services",
                table: "VendorType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "Vendor",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "Vendor",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Services",
                table: "Vendor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "UnitTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "UnitTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Unit",
                table: "UnitTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "UnitType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "UnitType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Unit",
                table: "UnitType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "Unit",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "Unit",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Unit",
                table: "Unit",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "TagTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "TagTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Tag",
                table: "TagTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "TagType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "TagType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Tag",
                table: "TagType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "Tag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "Tag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Tag",
                table: "Tag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "SchedulePriorityType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "SchedulePriorityType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "SchedulePriorityType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "SchedulePriority",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "SchedulePriority",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "SchedulePriority",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "Schedule",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "Schedule",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "Schedule",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyService",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyService",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyService",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyMember",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyMember",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

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
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "Pharmacy",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "Pharmacy",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "Pharmacy",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientReminder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientReminder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientReminder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientLaboratory",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientLaboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientLaboratory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientConsultation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientConsultation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientConsultation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientAilmentDetail",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientAilmentDetail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientAilmentDetail",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientAilment",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientAilment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientAilment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "Patient",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "Patient",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Patient",
                table: "Patient",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineVendor",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineVendor",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineVendor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineIntakeType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineIntakeType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineIntakeType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineIntake",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineIntake",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineIntake",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "Medicine",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "Medicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Medicine",
                table: "Medicine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRiderTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRiderTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRiderTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRider",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRider",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRider",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrderLocation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrderLocation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrderLocation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "Logistic",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "Logistic",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Logistic",
                table: "Logistic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryService",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryService",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryService",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryMember",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryMember",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "Laboratory",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "Laboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "Laboratory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalService",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalService",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalService",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalLocation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalLocation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalLocation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalLaboratory",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalLaboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalLaboratory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalConsultation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalConsultation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalConsultation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "Hospital",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "Hospital",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Hospital",
                table: "Hospital",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorConsultation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorConsultation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorConsultation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "Doctor",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "Doctor",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Doctor",
                table: "Doctor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

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

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "Consultation",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "Consultation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Consultation",
                table: "Consultation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "Availability",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "Availability",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Schedule",
                table: "Availability",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentTypeGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentTypeGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentTypeGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentTag",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentTag",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentTag",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "Ailment",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "Ailment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Ailment",
                table: "Ailment",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "VendorTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Services",
                table: "VendorTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "VendorType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Services",
                table: "VendorType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Services",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Services",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "UnitTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Unit",
                table: "UnitTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "UnitType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Unit",
                table: "UnitType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Unit",
                table: "Unit");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Unit",
                table: "Unit");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "TagTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Tag",
                table: "TagTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "TagType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Tag",
                table: "TagType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Tag",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Tag",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "ScheduleTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "ScheduleTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "SchedulePriorityType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "SchedulePriorityType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "SchedulePriority");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "SchedulePriority");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyStocks");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyServiceTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyService");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyService");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyMember");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyMember");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyLocation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "PharmacyJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Pharmacy",
                table: "Pharmacy");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Pharmacy",
                table: "Pharmacy");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientReminder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientReminder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientLaboratory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientLaboratory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientConsultation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientConsultation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientAilmentDetail");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientAilmentDetail");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "PatientAilment");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "PatientAilment");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Patient",
                table: "Patient");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Patient",
                table: "Patient");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineVendor");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineVendor");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineIntakeType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineIntakeType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "MedicineIntake");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "MedicineIntake");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Medicine",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Medicine",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRiderTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRiderTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRiderHandle");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRiderHandle");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticRider");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticRider");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrderLocation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrderLocation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrderDetail");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrderDetail");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "LogisticJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "LogisticJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Logistic",
                table: "Logistic");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Logistic",
                table: "Logistic");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryServiceTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryService");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryService");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryMember");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryMember");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryLocationTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryLocationTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryLocation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryLocation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "LaboratoryJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Laboratory",
                table: "Laboratory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Laboratory",
                table: "Laboratory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalServiceTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalServiceTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalService");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalService");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalLocation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalLocation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalLaboratory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalLaboratory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "HospitalConsultation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "HospitalConsultation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Hospital",
                table: "Hospital");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Hospital",
                table: "Hospital");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "DoctorConsultation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "DoctorConsultation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Doctor",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "ConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "ConsultationJobOrder");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Consultation",
                table: "Consultation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Consultation",
                table: "Consultation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Schedule",
                table: "Availability");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Schedule",
                table: "Availability");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentTypeGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentTypeGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "AilmentTag");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Ailment",
                table: "AilmentTag");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Ailment",
                table: "Ailment");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Ailment",
                table: "Ailment");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "VendorTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "VendorType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Services",
                table: "Vendor",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "UnitTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "UnitType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Unit",
                table: "Unit",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "TagTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "TagType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Tag",
                table: "Tag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "ScheduleTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "SchedulePriorityType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "SchedulePriority",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "Schedule",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyService",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyMember",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Pharmacy",
                table: "Pharmacy",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientReminder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientLaboratory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientConsultation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientAilmentDetail",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "PatientAilment",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Patient",
                table: "Patient",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineVendor",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineIntakeType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "MedicineIntake",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Medicine",
                table: "Medicine",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRiderTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticRider",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrderLocation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "LogisticJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Logistic",
                table: "Logistic",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryService",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryMember",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Laboratory",
                table: "Laboratory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalServiceTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalService",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalLocation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalLaboratory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "HospitalConsultation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Hospital",
                table: "Hospital",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "DoctorConsultation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Doctor",
                table: "Doctor",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Consultation",
                table: "Consultation",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Schedule",
                table: "Availability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentTypeGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "AilmentTag",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Ailment",
                table: "Ailment",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");
        }
    }
}
