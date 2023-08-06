using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthEssentials.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Ailment");

            migrationBuilder.EnsureSchema(
                name: "Schedule");

            migrationBuilder.EnsureSchema(
                name: "Consultation");

            migrationBuilder.EnsureSchema(
                name: "Doctor");

            migrationBuilder.EnsureSchema(
                name: "Hospital");

            migrationBuilder.EnsureSchema(
                name: "Laboratory");

            migrationBuilder.EnsureSchema(
                name: "Logistic");

            migrationBuilder.EnsureSchema(
                name: "Medicine");

            migrationBuilder.EnsureSchema(
                name: "MetaData");

            migrationBuilder.EnsureSchema(
                name: "Patient");

            migrationBuilder.EnsureSchema(
                name: "Pharmacy");

            migrationBuilder.EnsureSchema(
                name: "Tag");

            migrationBuilder.EnsureSchema(
                name: "Unit");

            migrationBuilder.EnsureSchema(
                name: "Services");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "AilmentTypeGroup",
                schema: "Ailment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ailmententitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationTypeGroup",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DoctorTypeGroup",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctorentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HospitalServiceTypeGroup",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalserviceentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HospitalTypeGroup",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryServiceTypeGroup",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryserviceentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryTypeGroup",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LogisticRider",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialGuid = table.Column<string>(type: "character varying", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    VehicleType = table.Column<string>(type: "character varying", nullable: true),
                    PlateNumber = table.Column<string>(type: "character varying", nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying", nullable: true),
                    LicenseExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticrider_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LogisticType",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MedicineIntakeType",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medicineintakeentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MedicineTypeGroup",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medicineentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MetaDataTypeGroup",
                schema: "MetaData",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadataentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientTypeGroup",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patiententitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyServiceTypeGroup",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyserviceentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyType",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SchedulePriorityType",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("schedulepriorityentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTypeGroup",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("scheduleentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TagTypeGroup",
                schema: "Tag",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tagentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UnitTypeGroup",
                schema: "Unit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("unitentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VendorTypeGroup",
                schema: "Services",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vendorentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AilmentType",
                schema: "Ailment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ailmententity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "ailmententity_ailmententitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Ailment",
                        principalTable: "AilmentTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ConsultationType",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultationentity_consultationentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "DoctorType",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctorentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "doctorentity_doctorentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Doctor",
                        principalTable: "DoctorTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalServiceType",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalserviceentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalserviceentity_hospitalserviceentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Hospital",
                        principalTable: "HospitalServiceTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalType",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalentity_hospitalentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Hospital",
                        principalTable: "HospitalTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryServiceType",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryserviceentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryserviceentity_laboratoryserviceentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryServiceTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryType",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryentity_laboratoryentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Logistic",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    Logo = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logistic_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logistic_logisticentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Logistic",
                        principalTable: "LogisticType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MedicineType",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadataentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "medicineentity_medicineentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Medicine",
                        principalTable: "MedicineTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MetaDataType",
                schema: "MetaData",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadataentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "metadataentity_metadataentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "MetaData",
                        principalTable: "MetaDataTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientType",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patiententity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patiententity_patiententitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Patient",
                        principalTable: "PatientTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PharmacyServiceType",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyserviceentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyserviceentity_pharmacyserviceentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyServiceTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Pharmacy",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Slogan = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    ChemicalComponent = table.Column<string>(type: "character varying", nullable: true),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    Logo = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacy_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacy_pharmacyentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "SchedulePriority",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("schedulepriority_pk", x => x.ID);
                    table.ForeignKey(
                        name: "schedulepriority_schedulepriorityentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Schedule",
                        principalTable: "SchedulePriorityType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ScheduleType",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("scheduleentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "scheduleentity_scheduleentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Schedule",
                        principalTable: "ScheduleTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "TagType",
                schema: "Tag",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tagentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tagentity_tagentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Tag",
                        principalTable: "TagTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "UnitType",
                schema: "Unit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("unitentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "unitentity_unitentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Unit",
                        principalTable: "UnitTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "VendorType",
                schema: "Services",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vendorentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "vendorentity_vendorentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Services",
                        principalTable: "VendorTypeGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Ailment",
                schema: "Ailment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    OtherName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ailment_pk", x => x.ID);
                    table.ForeignKey(
                        name: "ailment_ailmententity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Ailment",
                        principalTable: "AilmentType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Consultation",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultation_consultationentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Doctor",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialGuid = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    Clinic = table.Column<string>(type: "character varying", nullable: true),
                    ClinicAddress = table.Column<string>(type: "character varying", nullable: true),
                    BaseFee = table.Column<decimal>(type: "numeric", nullable: true),
                    PrcNumber = table.Column<string>(type: "character varying", nullable: true),
                    PtrNumber = table.Column<string>(type: "character varying", nullable: true),
                    PhilHealthNumber = table.Column<string>(type: "character varying", nullable: true),
                    TinNumber = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctor_pk", x => x.ID);
                    table.ForeignKey(
                        name: "doctor_doctorentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Doctor",
                        principalTable: "DoctorType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Hospital",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    Logo = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospital_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospital_hospitalentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Hospital",
                        principalTable: "HospitalType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Laboratory",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    Logo = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratory_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratory_laboratoryentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LogisticRiderHandle",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LogisticID = table.Column<Guid>(type: "uuid", nullable: false),
                    LogisticRiderID = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticriderhandle_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logisticriderhandle_logistic_id_fk",
                        column: x => x.LogisticID,
                        principalSchema: "Logistic",
                        principalTable: "Logistic",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "logisticriderhandle_logisticrider_id_fk",
                        column: x => x.LogisticRiderID,
                        principalSchema: "Logistic",
                        principalTable: "LogisticRider",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Medicine",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    ScientificName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    ChemicalComponent = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medicine_pk", x => x.ID);
                    table.ForeignKey(
                        name: "medicine_medicineentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Medicine",
                        principalTable: "MedicineType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MetaData",
                schema: "MetaData",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadata_pk", x => x.ID);
                    table.ForeignKey(
                        name: "metadata_metadataentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "MetaData",
                        principalTable: "MetaDataType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Patient",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialGuid = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patient_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patient_patiententity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Patient",
                        principalTable: "PatientType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PharmacyLocation",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayGuid = table.Column<string>(type: "character varying", nullable: true),
                    CityGuid = table.Column<string>(type: "character varying", nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionGuid = table.Column<string>(type: "character varying", nullable: true),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceGuid = table.Column<string>(type: "character varying", nullable: true),
                    CountryGuid = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    AlternativePhone = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacylocation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacylocation_pharmacy_id_fk",
                        column: x => x.PharmacyId,
                        principalSchema: "Pharmacy",
                        principalTable: "Pharmacy",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Availability",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    DateStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true")
                },
                constraints: table =>
                {
                    table.PrimaryKey("availability_pk", x => x.ID);
                    table.ForeignKey(
                        name: "schedule_scheduleentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Schedule",
                        principalTable: "ScheduleType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Schedule",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    PriorityID = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("schedule_pk", x => x.ID);
                    table.ForeignKey(
                        name: "schedule_scheduleentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Schedule",
                        principalTable: "ScheduleType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "schedule_schedulepriority_id_fk",
                        column: x => x.PriorityID,
                        principalSchema: "Schedule",
                        principalTable: "SchedulePriority",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                schema: "Tag",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tag_tagentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Tag",
                        principalTable: "TagType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Unit",
                schema: "Unit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("unit_pk", x => x.ID);
                    table.ForeignKey(
                        name: "unit_unitentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Unit",
                        principalTable: "UnitType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Vendor",
                schema: "Services",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    IsGenericProvider = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadata_pk", x => x.ID);
                    table.ForeignKey(
                        name: "vendor_vendorentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Services",
                        principalTable: "VendorType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "DoctorConsultation",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true, defaultValueSql: "0"),
                    MaxDiscount = table.Column<decimal>(type: "numeric", nullable: true, defaultValueSql: "0"),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctorconsultation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "doctorconsultation_consultation_id_fk",
                        column: x => x.ConsultationId,
                        principalSchema: "Consultation",
                        principalTable: "Consultation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "doctorconsultation_doctor_id_fk",
                        column: x => x.DoctorId,
                        principalSchema: "Doctor",
                        principalTable: "Doctor",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalConsultation",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalconsultation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalconsultation_consultation_id_fk",
                        column: x => x.ConsultationId,
                        principalSchema: "Consultation",
                        principalTable: "Consultation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "hospitalconsultation_hospital_id_fk",
                        column: x => x.HospitalId,
                        principalSchema: "Hospital",
                        principalTable: "Hospital",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalLocation",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayGuid = table.Column<string>(type: "character varying", nullable: true),
                    CityGuid = table.Column<string>(type: "character varying", nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionGuid = table.Column<string>(type: "character varying", nullable: true),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceGuid = table.Column<string>(type: "character varying", nullable: true),
                    CountryGuid = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitallocation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitallocation_hospital_id_fk",
                        column: x => x.HospitalId,
                        principalSchema: "Hospital",
                        principalTable: "Hospital",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalLaboratory",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitallaboratory_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalconsultation_hospital_id_fk",
                        column: x => x.HospitalId,
                        principalSchema: "Hospital",
                        principalTable: "Hospital",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "hospitallaboratory_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryLocation",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayGuid = table.Column<string>(type: "character varying", nullable: true),
                    CityGuid = table.Column<string>(type: "character varying", nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionGuid = table.Column<string>(type: "character varying", nullable: true),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceGuid = table.Column<string>(type: "character varying", nullable: true),
                    CountryGuid = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    Phone = table.Column<string>(type: "character varying", nullable: true),
                    Email = table.Column<string>(type: "character varying", nullable: true),
                    Website = table.Column<string>(type: "character varying", nullable: true),
                    AlternativePhone = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratorylocation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorylocation_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientAilment",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AilmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patientailment_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patientailment_ailment_id_fk",
                        column: x => x.AilmentId,
                        principalSchema: "Ailment",
                        principalTable: "Ailment",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "patientailment_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PharmacyMember",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialGuid = table.Column<string>(type: "character varying", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    PharmacyLocationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacymember_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacymember_pharmacy_id_fk",
                        column: x => x.PharmacyId,
                        principalSchema: "Pharmacy",
                        principalTable: "Pharmacy",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacymember_pharmacylocation_id_fk",
                        column: x => x.PharmacyLocationId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationJobOrder",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    PaymentStatus = table.Column<short>(type: "smallint", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    DiscountType = table.Column<decimal>(type: "numeric", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Prescription = table.Column<string>(type: "character varying", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying", nullable: true),
                    Treatment = table.Column<string>(type: "character varying", nullable: true),
                    Symptoms = table.Column<string>(type: "character varying", nullable: true),
                    MeetingLink = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultationjoborder_consultation_id_fk",
                        column: x => x.ConsultationId,
                        principalSchema: "Consultation",
                        principalTable: "Consultation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationjoborder_schedule_id_fk",
                        column: x => x.ScheduleId,
                        principalSchema: "Schedule",
                        principalTable: "Schedule",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LogisticJobOrder",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RiderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logisticjoborder_logisticrider_id_fk",
                        column: x => x.RiderId,
                        principalSchema: "Logistic",
                        principalTable: "LogisticRider",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "logisticjoborder_schedule_id_fk",
                        column: x => x.ScheduleId,
                        principalSchema: "Schedule",
                        principalTable: "Schedule",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyJobOrder",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    PaymentStatus = table.Column<short>(type: "smallint", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    DiscountType = table.Column<decimal>(type: "numeric", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrescriptionNote = table.Column<string>(type: "character varying", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyjoborder_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pharmacyjoborder_pharmacylocation_id_fk",
                        column: x => x.PharmacyLocationId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyjoborder_schedule_id_fk",
                        column: x => x.ScheduleId,
                        principalSchema: "Schedule",
                        principalTable: "Schedule",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "AilmentTag",
                schema: "Ailment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    AilmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadata_pk", x => x.ID);
                    table.ForeignKey(
                        name: "ailmenttag_ailment_id_fk",
                        column: x => x.AilmentId,
                        principalSchema: "Ailment",
                        principalTable: "Ailment",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "ailmenttag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ConsultationTag",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationtag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultationtag_pharmacy_id_fk",
                        column: x => x.ConsultationId,
                        principalSchema: "Consultation",
                        principalTable: "Consultation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationtag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorTag",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctortag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "doctor_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "doctortag_doctor_id_fk",
                        column: x => x.DoctorId,
                        principalSchema: "Doctor",
                        principalTable: "Doctor",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalTag",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitaltag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospital_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "hospitaltag_hospital_id_fk",
                        column: x => x.HospitalId,
                        principalSchema: "Hospital",
                        principalTable: "Hospital",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryTag",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacytag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorytag_pharmacy_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacytag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogisticRiderTag",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LogisticRiderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticridertag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logisticrider_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "logistictag_logisticrider_id_fk",
                        column: x => x.LogisticRiderId,
                        principalSchema: "Logistic",
                        principalTable: "LogisticRider",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MedicineTag",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadata_pk", x => x.ID);
                    table.ForeignKey(
                        name: "medicinetag_medicine_id_fk",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "medicinetag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientTag",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacytag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patient_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "patienttag_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PharmacyTag",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacytag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacytag_pharmacy_id_fk",
                        column: x => x.PharmacyId,
                        principalSchema: "Pharmacy",
                        principalTable: "Pharmacy",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacytag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTag",
                schema: "Schedule",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadata_pk", x => x.ID);
                    table.ForeignKey(
                        name: "scheduletag_schedule_id_fk",
                        column: x => x.ScheduleId,
                        principalSchema: "Schedule",
                        principalTable: "Schedule",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "scheduletag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MedicineIntake",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ScientificName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Repetition = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medicineintake_pk", x => x.ID);
                    table.ForeignKey(
                        name: "medicineintake_medicineintakeentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Medicine",
                        principalTable: "MedicineIntakeType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "medicineintake_unit_id_fk",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyService",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    PharmacyLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "numeric", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyservice_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyservice_pharmacy_id_fk",
                        column: x => x.PharmacyId,
                        principalSchema: "Pharmacy",
                        principalTable: "Pharmacy",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyservice_pharmacylocation_id_fk",
                        column: x => x.PharmacyLocationId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyservice_pharmacyserviceentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyServiceType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyservice_unit_id_fk",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyStocks",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastRestock = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AvailableQuantity = table.Column<Guid>(type: "uuid", nullable: false),
                    CriticalQuantity = table.Column<Guid>(type: "uuid", nullable: false),
                    MinQuantity = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxQuantity = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacystocks_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacystocks_medicine_id_fk",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacystocks_pharmacy_id_fk",
                        column: x => x.PharmacyId,
                        principalSchema: "Pharmacy",
                        principalTable: "Pharmacy",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacystocks_unit_id_fk",
                        column: x => x.Unit,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MedicineVendor",
                schema: "Medicine",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medicinevendor_pk", x => x.ID);
                    table.ForeignKey(
                        name: "medicinetag_medicine_id_fk",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "medicinevendor_vendor_id_fk",
                        column: x => x.VendorId,
                        principalSchema: "Services",
                        principalTable: "Vendor",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "HospitalService",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "numeric", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalservice_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalservice_hospital_id_fk",
                        column: x => x.HospitalId,
                        principalSchema: "Hospital",
                        principalTable: "Hospital",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "hospitalservice_hospitallocation_id_fk",
                        column: x => x.HospitalLocationId,
                        principalSchema: "Hospital",
                        principalTable: "HospitalLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "hospitalservice_hospitalserviceentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Hospital",
                        principalTable: "HospitalServiceType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "hospitalservice_unit_id_fk",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryLocationTag",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratorylocationtag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorylocationtag_laboratorylocation_id_fk",
                        column: x => x.LaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratorylocationtag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryMember",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialGuid = table.Column<string>(type: "character varying", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    LaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratorymember_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratorymember_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratorymember_laboratorylocation_id_fk",
                        column: x => x.LaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryService",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ShortName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "numeric", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryservice_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryservice_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryservice_laboratorylocation_id_fk",
                        column: x => x.LaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryservice_laboratoryserviceentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryServiceType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryservice_unit_id_fk",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationJobOrderLaboratory",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedLaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<string>(type: "character varying", nullable: true),
                    PrescriptionNote = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationjoborderlaboratory_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultationjoborderlaboratory_consultationjoborder_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationjoborderlaboratory_laboratorylocation_id_fk",
                        column: x => x.SuggestedLaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "consultationjoborderlaboratory_laboratoryserviceentity_id_fk",
                        column: x => x.LaboratoryServiceId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryServiceType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "DoctorConsultationJobOrder",
                schema: "Doctor",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("doctorconsultationjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "doctorconsultation_consultationjoborder_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "doctorconsultation_doctor_id_fk",
                        column: x => x.DoctorId,
                        principalSchema: "Doctor",
                        principalTable: "Doctor",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryJobOrder",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    PaymentStatus = table.Column<short>(type: "smallint", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    DiscountType = table.Column<decimal>(type: "numeric", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryjoborder_consultationjoborder_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "laboratoryjoborder_laboratory_id_fk",
                        column: x => x.LaboratoryId,
                        principalSchema: "Laboratory",
                        principalTable: "Laboratory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryjoborder_laboratorylocation_id_fk",
                        column: x => x.LaboratoryLocationId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryLocation",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryjoborder_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "laboratoryjoborder_schedule_id_fk",
                        column: x => x.ScheduleId,
                        principalSchema: "Schedule",
                        principalTable: "Schedule",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientAilmentDetail",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientAilmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorName = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patientailmentdetail_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patientailment_ailment_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "patientailmentdetail_patientailment_id_fk",
                        column: x => x.PatientAilmentId,
                        principalSchema: "Patient",
                        principalTable: "PatientAilment",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientConsultation",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patientconsultation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patientconsultation_ailment_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "patientconsultation_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientReminder",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSeen = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patientreminder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "patientreminder_ailment_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "patientreminder_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LogisticJobOrderDetail",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LogisticJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    Quantity = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<Guid>(type: "uuid", nullable: false),
                    Discount = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountType = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationGuid = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticjoborderdetail_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logisticjoborderdetail_logisticjoborder_id_fk",
                        column: x => x.LogisticJobOrderId,
                        principalSchema: "Logistic",
                        principalTable: "LogisticJobOrder",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "logisticjoborderdetail_unit_id_fk",
                        column: x => x.UnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogisticJobOrderLocation",
                schema: "Logistic",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LogisticJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayGuid = table.Column<string>(type: "character varying", nullable: true),
                    CityGuid = table.Column<string>(type: "character varying", nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionGuid = table.Column<string>(type: "character varying", nullable: true),
                    MainAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceGuid = table.Column<string>(type: "character varying", nullable: true),
                    CountryGuid = table.Column<string>(type: "character varying", nullable: true),
                    Priority = table.Column<short>(type: "smallint", nullable: false),
                    IsDestination = table.Column<bool>(type: "boolean", nullable: false),
                    ClientGuid = table.Column<string>(type: "character varying", nullable: true),
                    ClientName = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("logisticjoborderlocation_pk", x => x.ID);
                    table.ForeignKey(
                        name: "logisticjoborderlocation_logisticjoborder_id_fk",
                        column: x => x.LogisticJobOrderId,
                        principalSchema: "Logistic",
                        principalTable: "LogisticJobOrder",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PharmacyJobOrderConsultationJobOrder",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyjoborderconsultationjoborder_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyjoborder_consultationjoborder_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_pharmacyjoborder_id_fk",
                        column: x => x.PharmacyJobOrderId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyJobOrder",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ConsultationJobOrderMedicine",
                schema: "Consultation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConsultationJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineIntakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionNote = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    IntakeRepetition = table.Column<int>(type: "integer", nullable: false),
                    IntakeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duration = table.Column<decimal>(type: "numeric(2)", precision: 2, nullable: false),
                    DurationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dosage = table.Column<int>(type: "integer", nullable: true),
                    DosageUnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("consultationjobordermedicine_pk", x => x.ID);
                    table.ForeignKey(
                        name: "consultationjobordermedicine_consultationjoborder_id_fk",
                        column: x => x.ConsultationJobOrderId,
                        principalSchema: "Consultation",
                        principalTable: "ConsultationJobOrder",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationjobordermedicine_medicine_id_fk",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "consultationjobordermedicine_medicineintake_id_fk",
                        column: x => x.MedicineIntakeId,
                        principalSchema: "Medicine",
                        principalTable: "MedicineIntake",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "consultationjobordermedicine_unit_id_fk",
                        column: x => x.IntakeUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationjobordermedicine_unit_id_fk_2",
                        column: x => x.DurationUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "consultationjobordermedicine_unit_id_fk_3",
                        column: x => x.DosageUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyJobOrderMedicine",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineIntakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionNote = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    IntakeRepetition = table.Column<int>(type: "integer", nullable: false),
                    IntakeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duration = table.Column<decimal>(type: "numeric(2)", precision: 2, nullable: false, defaultValueSql: "1"),
                    DurationUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dosage = table.Column<int>(type: "integer", nullable: true),
                    DosageUnitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyjobordermedicine_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_medicine_id_fk",
                        column: x => x.MedicineId,
                        principalSchema: "Medicine",
                        principalTable: "Medicine",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_medicineintake_id_fk",
                        column: x => x.MedicineIntakeId,
                        principalSchema: "Medicine",
                        principalTable: "MedicineIntake",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_pharmacyjoborder_id_fk",
                        column: x => x.PharmacyJobOrderId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyJobOrder",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_unit_id_fk",
                        column: x => x.IntakeUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_unit_id_fk_2",
                        column: x => x.DurationUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacyjobordermedicine_unit_id_fk_3",
                        column: x => x.DosageUnitId,
                        principalSchema: "Unit",
                        principalTable: "Unit",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyServiceTag",
                schema: "Pharmacy",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PharmacyServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pharmacyservicetag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "pharmacyservicetag_pharmacy_id_fk",
                        column: x => x.PharmacyServiceId,
                        principalSchema: "Pharmacy",
                        principalTable: "PharmacyService",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacytag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HospitalServiceTag",
                schema: "Hospital",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    HospitalServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hospitalservicetag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "hospitalservicetag_hospital_id_fk",
                        column: x => x.HospitalServiceId,
                        principalSchema: "Hospital",
                        principalTable: "HospitalService",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "hospitaltag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryServiceTag",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryservicetag_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryservicetag_pharmacy_id_fk",
                        column: x => x.LaboratoryServiceId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryService",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "pharmacytag_tag_id_fk",
                        column: x => x.TagId,
                        principalSchema: "Tag",
                        principalTable: "Tag",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryJobOrderDetail",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryjoborderdetail_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryjoborder_laboratoryservice_id_fk",
                        column: x => x.LaboratoryServiceId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryService",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "laboratoryjoborderdetail_laboratoryjoborder_id_fk",
                        column: x => x.LaboratoryJobOrderId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryJobOrder",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryJobOrderResult",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryJobOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryjoborderresult_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryjoborderresult_laboratoryjoborder_id_fk",
                        column: x => x.LaboratoryJobOrderId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryJobOrder",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "PatientLaboratory",
                schema: "Patient",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaboratoryJobOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("patientlaboratory_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryjoborder_ailment_id_fk",
                        column: x => x.LaboratoryJobOrderId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryJobOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "patientconsultation_patient_id_fk",
                        column: x => x.PatientId,
                        principalSchema: "Patient",
                        principalTable: "Patient",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryJobOrderResultFiles",
                schema: "Laboratory",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LaboratoryJobOrderResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageFileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("laboratoryjoborderresultfiles_pk", x => x.ID);
                    table.ForeignKey(
                        name: "laboratoryjoborderresultfiles_laboratoryjoborderresult_id_fk",
                        column: x => x.LaboratoryJobOrderResultId,
                        principalSchema: "Laboratory",
                        principalTable: "LaboratoryJobOrderResult",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ailment_TypeID",
                schema: "Ailment",
                table: "Ailment",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_AilmentTag_AilmentId",
                schema: "Ailment",
                table: "AilmentTag",
                column: "AilmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AilmentTag_TagId",
                schema: "Ailment",
                table: "AilmentTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_AilmentType_GroupId",
                schema: "Ailment",
                table: "AilmentType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Availability_TypeID",
                schema: "Schedule",
                table: "Availability",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Consultation_TypeID",
                schema: "Consultation",
                table: "Consultation",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrder_ConsultationId",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrder_ScheduleId",
                schema: "Consultation",
                table: "ConsultationJobOrder",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderLaboratory_ConsultationJobOrderId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderLaboratory_LaboratoryServiceId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "LaboratoryServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderLaboratory_SuggestedLaboratoryLocationId",
                schema: "Consultation",
                table: "ConsultationJobOrderLaboratory",
                column: "SuggestedLaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_ConsultationJobOrderId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_DosageUnitId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "DosageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_DurationUnitId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "DurationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_IntakeUnitId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "IntakeUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationJobOrderMedicine_MedicineIntakeId",
                schema: "Consultation",
                table: "ConsultationJobOrderMedicine",
                column: "MedicineIntakeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationTag_ConsultationId",
                schema: "Consultation",
                table: "ConsultationTag",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationTag_TagId",
                schema: "Consultation",
                table: "ConsultationTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationType_GroupId",
                schema: "Consultation",
                table: "ConsultationType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctor_TypeID",
                schema: "Doctor",
                table: "Doctor",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorConsultation_ConsultationId",
                schema: "Doctor",
                table: "DoctorConsultation",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorConsultation_DoctorId",
                schema: "Doctor",
                table: "DoctorConsultation",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorConsultationJobOrder_ConsultationJobOrderId",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorConsultationJobOrder_DoctorId",
                schema: "Doctor",
                table: "DoctorConsultationJobOrder",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorTag_DoctorId",
                schema: "Doctor",
                table: "DoctorTag",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorTag_TagId",
                schema: "Doctor",
                table: "DoctorTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorType_GroupId",
                schema: "Doctor",
                table: "DoctorType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Hospital_TypeID",
                schema: "Hospital",
                table: "Hospital",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalConsultation_ConsultationId",
                schema: "Hospital",
                table: "HospitalConsultation",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalConsultation_HospitalId",
                schema: "Hospital",
                table: "HospitalConsultation",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalLaboratory_HospitalId",
                schema: "Hospital",
                table: "HospitalLaboratory",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalLaboratory_LaboratoryId",
                schema: "Hospital",
                table: "HospitalLaboratory",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalLocation_HospitalId",
                schema: "Hospital",
                table: "HospitalLocation",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalService_HospitalId",
                schema: "Hospital",
                table: "HospitalService",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalService_HospitalLocationId",
                schema: "Hospital",
                table: "HospitalService",
                column: "HospitalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalService_TypeID",
                schema: "Hospital",
                table: "HospitalService",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalService_UnitId",
                schema: "Hospital",
                table: "HospitalService",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalServiceTag_HospitalServiceId",
                schema: "Hospital",
                table: "HospitalServiceTag",
                column: "HospitalServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalServiceTag_TagId",
                schema: "Hospital",
                table: "HospitalServiceTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalServiceType_GroupId",
                schema: "Hospital",
                table: "HospitalServiceType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalTag_HospitalId",
                schema: "Hospital",
                table: "HospitalTag",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalTag_TagId",
                schema: "Hospital",
                table: "HospitalTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalType_GroupId",
                schema: "Hospital",
                table: "HospitalType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Laboratory_TypeID",
                schema: "Laboratory",
                table: "Laboratory",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrder_ConsultationJobOrderId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrder_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrder_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "LaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrder_PatientId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrder_ScheduleId",
                schema: "Laboratory",
                table: "LaboratoryJobOrder",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrderDetail_LaboratoryJobOrderId",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                column: "LaboratoryJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrderDetail_LaboratoryServiceId",
                schema: "Laboratory",
                table: "LaboratoryJobOrderDetail",
                column: "LaboratoryServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrderResult_LaboratoryJobOrderId",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResult",
                column: "LaboratoryJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryJobOrderResultFiles_LaboratoryJobOrderResultId",
                schema: "Laboratory",
                table: "LaboratoryJobOrderResultFiles",
                column: "LaboratoryJobOrderResultId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocation_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryLocation",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocationTag_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                column: "LaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryLocationTag_TagId",
                schema: "Laboratory",
                table: "LaboratoryLocationTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryMember_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryMember_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryMember",
                column: "LaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryService_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryService_LaboratoryLocationId",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "LaboratoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryService_TypeID",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryService_UnitId",
                schema: "Laboratory",
                table: "LaboratoryService",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryServiceTag_LaboratoryServiceId",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                column: "LaboratoryServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryServiceTag_TagId",
                schema: "Laboratory",
                table: "LaboratoryServiceTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryServiceType_GroupId",
                schema: "Laboratory",
                table: "LaboratoryServiceType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryTag_LaboratoryId",
                schema: "Laboratory",
                table: "LaboratoryTag",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryTag_TagId",
                schema: "Laboratory",
                table: "LaboratoryTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryType_GroupId",
                schema: "Laboratory",
                table: "LaboratoryType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Logistic_TypeID",
                schema: "Logistic",
                table: "Logistic",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticJobOrder_RiderId",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticJobOrder_ScheduleId",
                schema: "Logistic",
                table: "LogisticJobOrder",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticJobOrderDetail_LogisticJobOrderId",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                column: "LogisticJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticJobOrderDetail_UnitId",
                schema: "Logistic",
                table: "LogisticJobOrderDetail",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticJobOrderLocation_LogisticJobOrderId",
                schema: "Logistic",
                table: "LogisticJobOrderLocation",
                column: "LogisticJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticRiderHandle_LogisticID",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                column: "LogisticID");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticRiderHandle_LogisticRiderID",
                schema: "Logistic",
                table: "LogisticRiderHandle",
                column: "LogisticRiderID");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticRiderTag_LogisticRiderId",
                schema: "Logistic",
                table: "LogisticRiderTag",
                column: "LogisticRiderId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticRiderTag_TagId",
                schema: "Logistic",
                table: "LogisticRiderTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Medicine_TypeId",
                schema: "Medicine",
                table: "Medicine",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineIntake_TypeId",
                schema: "Medicine",
                table: "MedicineIntake",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineIntake_UnitId",
                schema: "Medicine",
                table: "MedicineIntake",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineTag_MedicineId",
                schema: "Medicine",
                table: "MedicineTag",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineTag_TagId",
                schema: "Medicine",
                table: "MedicineTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineType_GroupId",
                schema: "Medicine",
                table: "MedicineType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineVendor_MedicineId",
                schema: "Medicine",
                table: "MedicineVendor",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineVendor_VendorId",
                schema: "Medicine",
                table: "MedicineVendor",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaData_TypeId",
                schema: "MetaData",
                table: "MetaData",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaDataType_GroupId",
                schema: "MetaData",
                table: "MetaDataType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Patient_TypeID",
                schema: "Patient",
                table: "Patient",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAilment_AilmentId",
                schema: "Patient",
                table: "PatientAilment",
                column: "AilmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAilment_PatientId",
                schema: "Patient",
                table: "PatientAilment",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAilmentDetail_ConsultationJobOrderId",
                schema: "Patient",
                table: "PatientAilmentDetail",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAilmentDetail_PatientAilmentId",
                schema: "Patient",
                table: "PatientAilmentDetail",
                column: "PatientAilmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientConsultation_ConsultationJobOrderId",
                schema: "Patient",
                table: "PatientConsultation",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientConsultation_PatientId",
                schema: "Patient",
                table: "PatientConsultation",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLaboratory_LaboratoryJobOrderId",
                schema: "Patient",
                table: "PatientLaboratory",
                column: "LaboratoryJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLaboratory_PatientId",
                schema: "Patient",
                table: "PatientLaboratory",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientReminder_ConsultationJobOrderId",
                schema: "Patient",
                table: "PatientReminder",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientReminder_PatientId",
                schema: "Patient",
                table: "PatientReminder",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTag_PatientId",
                schema: "Patient",
                table: "PatientTag",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTag_TagId",
                schema: "Patient",
                table: "PatientTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientType_GroupId",
                schema: "Patient",
                table: "PatientType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Pharmacy_TypeId",
                schema: "Pharmacy",
                table: "Pharmacy",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrder_PatientId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrder_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                column: "PharmacyLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrder_ScheduleId",
                schema: "Pharmacy",
                table: "PharmacyJobOrder",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderConsultationJobOrder_ConsultationJobOrderId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                column: "ConsultationJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderConsultationJobOrder_PharmacyJobOrderId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderConsultationJobOrder",
                column: "PharmacyJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_DosageUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "DosageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_DurationUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "DurationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_IntakeUnitId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "IntakeUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_MedicineIntakeId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "MedicineIntakeId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyJobOrderMedicine_PharmacyJobOrderId",
                schema: "Pharmacy",
                table: "PharmacyJobOrderMedicine",
                column: "PharmacyJobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyLocation_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyLocation",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyMember_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyMember_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyMember",
                column: "PharmacyLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyService_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyService",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyService_PharmacyLocationId",
                schema: "Pharmacy",
                table: "PharmacyService",
                column: "PharmacyLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyService_TypeID",
                schema: "Pharmacy",
                table: "PharmacyService",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyService_UnitId",
                schema: "Pharmacy",
                table: "PharmacyService",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyServiceTag_PharmacyServiceId",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                column: "PharmacyServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyServiceTag_TagId",
                schema: "Pharmacy",
                table: "PharmacyServiceTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyServiceType_GroupId",
                schema: "Pharmacy",
                table: "PharmacyServiceType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStocks_MedicineId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStocks_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStocks_Unit",
                schema: "Pharmacy",
                table: "PharmacyStocks",
                column: "Unit");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyTag_PharmacyId",
                schema: "Pharmacy",
                table: "PharmacyTag",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyTag_TagId",
                schema: "Pharmacy",
                table: "PharmacyTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_PriorityID",
                schema: "Schedule",
                table: "Schedule",
                column: "PriorityID");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_TypeID",
                schema: "Schedule",
                table: "Schedule",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePriority_TypeID",
                schema: "Schedule",
                table: "SchedulePriority",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTag_ScheduleId",
                schema: "Schedule",
                table: "ScheduleTag",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTag_TagId",
                schema: "Schedule",
                table: "ScheduleTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleType_GroupId",
                schema: "Schedule",
                table: "ScheduleType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_TypeID",
                schema: "Tag",
                table: "Tag",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_TagType_GroupId",
                schema: "Tag",
                table: "TagType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_TypeID",
                schema: "Unit",
                table: "Unit",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_UnitType_GroupId",
                schema: "Unit",
                table: "UnitType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_TypeID",
                schema: "Services",
                table: "Vendor",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_VendorType_GroupId",
                schema: "Services",
                table: "VendorType",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AilmentTag",
                schema: "Ailment");

            migrationBuilder.DropTable(
                name: "Availability",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "ConsultationJobOrderLaboratory",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "ConsultationJobOrderMedicine",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "ConsultationTag",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "DoctorConsultation",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "DoctorConsultationJobOrder",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "DoctorTag",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "HospitalConsultation",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "HospitalLaboratory",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "HospitalServiceTag",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "HospitalTag",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "LaboratoryJobOrderDetail",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryJobOrderResultFiles",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryLocationTag",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryMember",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryServiceTag",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryTag",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LogisticJobOrderDetail",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "LogisticJobOrderLocation",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "LogisticRiderHandle",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "LogisticRiderTag",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "MedicineTag",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "MedicineVendor",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "MetaData",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "PatientAilmentDetail",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "PatientConsultation",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "PatientLaboratory",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "PatientReminder",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "PatientTag",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "PharmacyJobOrderConsultationJobOrder",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyJobOrderMedicine",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyMember",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyServiceTag",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyStocks",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyTag",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "ScheduleTag",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "Doctor",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "HospitalService",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "LaboratoryJobOrderResult",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryService",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LogisticJobOrder",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "Logistic",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "Vendor",
                schema: "Services");

            migrationBuilder.DropTable(
                name: "MetaDataType",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "PatientAilment",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "MedicineIntake",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "PharmacyJobOrder",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyService",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "Medicine",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "Tag",
                schema: "Tag");

            migrationBuilder.DropTable(
                name: "DoctorType",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "HospitalLocation",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "HospitalServiceType",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "LaboratoryJobOrder",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratoryServiceType",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "LogisticRider",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "LogisticType",
                schema: "Logistic");

            migrationBuilder.DropTable(
                name: "VendorType",
                schema: "Services");

            migrationBuilder.DropTable(
                name: "MetaDataTypeGroup",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "Ailment",
                schema: "Ailment");

            migrationBuilder.DropTable(
                name: "MedicineIntakeType",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "PharmacyLocation",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyServiceType",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "Unit",
                schema: "Unit");

            migrationBuilder.DropTable(
                name: "MedicineType",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "TagType",
                schema: "Tag");

            migrationBuilder.DropTable(
                name: "DoctorTypeGroup",
                schema: "Doctor");

            migrationBuilder.DropTable(
                name: "Hospital",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "HospitalServiceTypeGroup",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "ConsultationJobOrder",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "LaboratoryLocation",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "Patient",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "LaboratoryServiceTypeGroup",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "VendorTypeGroup",
                schema: "Services");

            migrationBuilder.DropTable(
                name: "AilmentType",
                schema: "Ailment");

            migrationBuilder.DropTable(
                name: "Pharmacy",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacyServiceTypeGroup",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "UnitType",
                schema: "Unit");

            migrationBuilder.DropTable(
                name: "MedicineTypeGroup",
                schema: "Medicine");

            migrationBuilder.DropTable(
                name: "TagTypeGroup",
                schema: "Tag");

            migrationBuilder.DropTable(
                name: "HospitalType",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "Consultation",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "Schedule",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "Laboratory",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "PatientType",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "AilmentTypeGroup",
                schema: "Ailment");

            migrationBuilder.DropTable(
                name: "PharmacyType",
                schema: "Pharmacy");

            migrationBuilder.DropTable(
                name: "UnitTypeGroup",
                schema: "Unit");

            migrationBuilder.DropTable(
                name: "HospitalTypeGroup",
                schema: "Hospital");

            migrationBuilder.DropTable(
                name: "ConsultationType",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "ScheduleType",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "SchedulePriority",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "LaboratoryType",
                schema: "Laboratory");

            migrationBuilder.DropTable(
                name: "PatientTypeGroup",
                schema: "Patient");

            migrationBuilder.DropTable(
                name: "ConsultationTypeGroup",
                schema: "Consultation");

            migrationBuilder.DropTable(
                name: "ScheduleTypeGroup",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "SchedulePriorityType",
                schema: "Schedule");

            migrationBuilder.DropTable(
                name: "LaboratoryTypeGroup",
                schema: "Laboratory");
        }
    }
}
