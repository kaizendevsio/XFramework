using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "GeoLocation");

            migrationBuilder.EnsureSchema(
                name: "Application");

            migrationBuilder.EnsureSchema(
                name: "Audit");

            migrationBuilder.EnsureSchema(
                name: "Integration.BillsPayment");

            migrationBuilder.EnsureSchema(
                name: "Affiliate");

            migrationBuilder.EnsureSchema(
                name: "BusinessPackage");

            migrationBuilder.EnsureSchema(
                name: "Community");

            migrationBuilder.EnsureSchema(
                name: "Finance");

            migrationBuilder.EnsureSchema(
                name: "Wallet");

            migrationBuilder.EnsureSchema(
                name: "Integration.ELoad");

            migrationBuilder.EnsureSchema(
                name: "Integration.PaymentGateway");

            migrationBuilder.EnsureSchema(
                name: "Identity");

            migrationBuilder.EnsureSchema(
                name: "Income");

            migrationBuilder.EnsureSchema(
                name: "Messaging");

            migrationBuilder.EnsureSchema(
                name: "MetaData");

            migrationBuilder.EnsureSchema(
                name: "Registry");

            migrationBuilder.EnsureSchema(
                name: "Storage");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "BusinessPackageInclusionsType",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsNumericValue = table.Column<bool>(type: "boolean", nullable: true),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    IconImage = table.Column<string>(type: "character varying", nullable: true),
                    Unit = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BusinessPackageInclusionsType_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackageType",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BusinessPackageType_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityConnectionType",
                schema: "Community",
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
                    table.PrimaryKey("socialmediaconnectionentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityContentReactionType",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialmediacontentreactionentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityContentType",
                schema: "Community",
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
                    table.PrimaryKey("socialmediacontententity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityIdentityFileType",
                schema: "Community",
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
                    table.PrimaryKey("communityidentityfileentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityIdentityType",
                schema: "Community",
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
                    table.PrimaryKey("communityidentityentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyType",
                schema: "Finance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrencyIsoCode3 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_currency_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Enterprise",
                schema: "Application",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_tbl_Enterprises", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCategory",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_gatewaycategories_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GatewayResponseStatusType",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gatewayresponsestatustype_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GatewayType",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_gatewayType_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityAddressType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityAddressType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityContactGroup",
                schema: "Identity",
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
                    table.PrimaryKey("identitycontactgroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityContactType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityContactType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityRoleEntityGroup",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("identityroleentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerificationType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultExpiry = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<short>(type: "smallint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_VerificationType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IncomeType",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IncomeTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IncomeTypeShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IncomeTypeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsReward = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_IncomeType_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageDeliveryType",
                schema: "Messaging",
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
                    table.PrimaryKey("messagedeliveryentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageReactionType",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagereactionentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageType",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagetype_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MetaDataEntityGroup",
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
                name: "ProviderType",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_providerType_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RegistryConfigurationGroup",
                schema: "Registry",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_configurationgroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RegistryFavoriteType",
                schema: "Registry",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_favoriteType_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SessionType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_SessionType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "StorageFileIdentifierGroup",
                schema: "Storage",
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
                name: "StorageFileType",
                schema: "Storage",
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
                    table.PrimaryKey("storagefileentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionType",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("subscriptionentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TelcoCodePromo",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_TelcoCodePromos", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TelcoType",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_TelcoType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AddressCountry",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsoCode2 = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsoCode3 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhoneCountryCode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    CurrencyID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_AddressCountry_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "CurrencyID",
                        column: x => x.CurrencyID,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRate",
                schema: "Finance",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    SourceCurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    EffectivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_ExchangeRate_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "SourceCurrencyID",
                        column: x => x.SourceCurrencyTypeID,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "TargetCurrencyID",
                        column: x => x.TargetCurrencyTypeID,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Application",
                schema: "Application",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    AppName = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    Expiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailabilityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentAppID = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnterpriseID = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Application", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_applications_tbl_enterprises_id_fk",
                        column: x => x.EnterpriseID,
                        principalSchema: "Application",
                        principalTable: "Enterprise",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "GatewayEndpoint",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    GatewayID = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaseUrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    UrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_gatewayendpoints_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_gatewayendpoints_tbl_gatewayType_id_fk",
                        column: x => x.GatewayID,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "GatewayResponseType",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: false),
                    GatewayTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gatewayresponsetype_pk", x => x.ID);
                    table.ForeignKey(
                        name: "gatewayresponsetype_gatewayTypes_id_fk",
                        column: x => x.GatewayTypeId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThreadType",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    MessageTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethreadentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagethreadentity_messagetype_id_fk",
                        column: x => x.MessageTypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageType",
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
                        principalTable: "MetaDataEntityGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BillerCategory",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProviderTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_billercategories_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_billercategories_tbl_providerType_id_fk",
                        column: x => x.ProviderTypeID,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "ProviderType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ProviderEndpoint",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProviderID = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaseUrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    UrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_providerendpoints_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_providerendpoints_tbl_providerType_id_fk",
                        column: x => x.ProviderID,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "ProviderType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "StorageFileIdentifier",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storagefileidentifier_pk", x => x.ID);
                    table.ForeignKey(
                        name: "storagefileidentifier_storagefileidentifiergroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Storage",
                        principalTable: "StorageFileIdentifierGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "GatewayNumber",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    TelcoTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Gateway = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_gatewaynumbers_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_telcoType_id_fk",
                        column: x => x.TelcoTypeID,
                        principalSchema: "Integration.ELoad",
                        principalTable: "TelcoType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "TelcoPrefix",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    TelcoTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Prefix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_TelcoPrefix", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_telcoentityid___fk",
                        column: x => x.TelcoTypeID,
                        principalSchema: "Integration.ELoad",
                        principalTable: "TelcoType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelcoProductCode",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Validity = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    TelcoTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    TelcoCodePromosID = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_TelcoProductCode", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_telcoproductcode_tbl_telcoType_id_fk",
                        column: x => x.TelcoTypeID,
                        principalSchema: "Integration.ELoad",
                        principalTable: "TelcoType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_telcoproductcode_tbl_telcocodepromos_id_fk",
                        column: x => x.TelcoCodePromosID,
                        principalSchema: "Integration.ELoad",
                        principalTable: "TelcoCodePromo",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AddressRegion",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PsgcCode = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CountryID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_addressregions_pk", x => x.ID);
                    table.UniqueConstraint("AK_AddressRegion_Code", x => x.Code);
                    table.ForeignKey(
                        name: "tbl_addressregions_tbl_addresscountry_id_fk",
                        column: x => x.CountryID,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCountry",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityInformation",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdentityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdentityDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<short>(type: "smallint", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CivilStatus = table.Column<short>(type: "smallint", nullable: true),
                    ApplicationID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityInfo", x => x.ID);
                    table.ForeignKey(
                        name: "identityinformation_application_id_fk",
                        column: x => x.ApplicationID,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IdentityRoleType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoleLevel = table.Column<short>(type: "smallint", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityRoleType", x => x.ID);
                    table.ForeignKey(
                        name: "identityroleentity_identityroleentitygroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Identity",
                        principalTable: "IdentityRoleEntityGroup",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_identityroleTypes_tbl_applications_id_fk",
                        column: x => x.ApplicationId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Log",
                schema: "Audit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ApplicationID = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<short>(type: "smallint", nullable: true),
                    Message = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Initiator = table.Column<string>(type: "character varying", nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Seen = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ApplicationLogs", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_applogs_appid_fk",
                        column: x => x.ApplicationID,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistryConfiguration",
                schema: "Registry",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ApplicationID = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_applicationconfiguration_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_applicationconfiguration_tbl_application_id_fk",
                        column: x => x.ApplicationID,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_configurations_tbl_configurationgroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Registry",
                        principalTable: "RegistryConfigurationGroup",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletType",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Desc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    CurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    MinTransfer = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxTransfer = table.Column<decimal>(type: "numeric", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletType_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "CurrencyID",
                        column: x => x.CurrencyTypeID,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_walletTypes_tbl_applications_id_fk",
                        column: x => x.ApplicationId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Gateway",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    GatewayCategoryID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    ServiceCharge = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Image = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderEndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_gateways_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_gateways_tbl_gatewaycategories_id_fk",
                        column: x => x.GatewayCategoryID,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayCategory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_gateways_tbl_providerendpoints_id_fk",
                        column: x => x.ProviderEndpointId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayEndpoint",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "GatewayResponse",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: false),
                    Message = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    ResponseStatusTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayResponseTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gatewayresponse_pk", x => x.ID);
                    table.ForeignKey(
                        name: "gatewayresponse_gatewayresponsestatustype_id_fk",
                        column: x => x.ResponseStatusTypeId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayResponseStatusType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "gatewayresponse_gatewayresponsetype_id_fk",
                        column: x => x.GatewayResponseTypeId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "GatewayResponseType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThread",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethread_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagethread_messagethreadentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThreadType",
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
                name: "Biller",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BillerCategoryID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    ServiceCharge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Image = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderEndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_billers_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_billers_tbl_billercategories_id_fk",
                        column: x => x.BillerCategoryID,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "BillerCategory",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_billers_tbl_providerendpoints_id_fk",
                        column: x => x.ProviderEndpointId,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "ProviderEndpoint",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "StorageFile",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ContentPath = table.Column<string>(type: "character varying", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<Guid>(type: "uuid", nullable: false),
                    FileSize = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StorageFileIdentifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ContentType = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("storagefile_pk", x => x.ID);
                    table.ForeignKey(
                        name: "storagefile_storagefileentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Storage",
                        principalTable: "StorageFileType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "storagefile_storagefileidentifier_id_fk",
                        column: x => x.StorageFileIdentifierId,
                        principalSchema: "Storage",
                        principalTable: "StorageFileIdentifier",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AddressProvince",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PsgcCode = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RegCodeId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_addressprovince_pk", x => x.ID);
                    table.UniqueConstraint("AK_AddressProvince_Code", x => x.Code);
                    table.ForeignKey(
                        name: "tbl_addressprovince_tbl_addressregions_code_fk",
                        column: x => x.RegCodeId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressRegion",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "IdentityCredential",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityInfoID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAlias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LogInStatus = table.Column<short>(type: "smallint", nullable: true),
                    PasswordByte = table.Column<byte[]>(type: "bytea", nullable: true),
                    ApplicationID = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityCredentials", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_identitycredentials___fk",
                        column: x => x.ApplicationID,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identitycredentials_fk",
                        column: x => x.IdentityInfoID,
                        principalSchema: "Identity",
                        principalTable: "IdentityInformation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatewayInstructions",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GatewayId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstructionText = table.Column<string>(type: "character varying", nullable: true),
                    ExampleText = table.Column<string>(type: "character varying", nullable: true),
                    StepOrder = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("GatewayInstructions_pk", x => x.Id);
                    table.ForeignKey(
                        name: "GatewayInstructions_Gateways_ID_fk",
                        column: x => x.GatewayId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "Gateway",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThreadMemberGroup",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethreadmembergroup_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagethreadmembergroup_messagethread_id_fk",
                        column: x => x.MessageThreadId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThread",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BillerField",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BillerID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Type = table.Column<string>(type: "character varying", nullable: false),
                    Width = table.Column<decimal>(type: "numeric(2)", precision: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_billerfields_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_billerfields_tbl_billers_id_fk",
                        column: x => x.BillerID,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "Biller",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "AddressCity",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PsgcCode = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ProvCodeId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<long>(type: "bigint", nullable: false),
                    RegCode = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_addresscity_pk", x => x.ID);
                    table.UniqueConstraint("AK_AddressCity_Code", x => x.Code);
                    table.ForeignKey(
                        name: "tbl_addresscity_tbl_addressprovince_code_fk",
                        column: x => x.ProvCodeId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressProvince",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationLog",
                schema: "Audit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IPAddress = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: true),
                    AuthStatus = table.Column<short>(type: "smallint", nullable: true),
                    LoginSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityAuthorizationLogs", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_userauthhistory_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityIdentity",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandleName = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastActive = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: true),
                    Tagline = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialidentity_pk", x => x.ID);
                    table.ForeignKey(
                        name: "communityidentity_communityidentityentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentityType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialidentity_identitycredential_id_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepositRequest",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Address = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DepositStatus = table.Column<short>(type: "smallint", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawRequestData = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    RawResponseData = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    SystemFee = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    DiscountType = table.Column<int>(type: "integer", nullable: true),
                    GatewayId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_DepositRequests_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "DepositRequest_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "DepositRequest_Gateway_ID_fk",
                        column: x => x.GatewayId,
                        principalSchema: "Integration.PaymentGateway",
                        principalTable: "Gateway",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "DepositRequest_WalletTypeId",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "SourceCurrencyId",
                        column: x => x.SourceCurrencyId,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "ELoadTransaction",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    TelcoProductCodeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    CustomerNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    TransactionID = table.Column<string>(type: "character varying", nullable: true),
                    WalletTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    RawRequest = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    RawResponse = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_usereloadtransaction_pk", x => x.ID);
                    table.ForeignKey(
                        name: "EloadTransaction_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_usereloadtransaction_tbl_telcoproductcode_id_fk",
                        column: x => x.TelcoProductCodeID,
                        principalSchema: "Integration.ELoad",
                        principalTable: "TelcoProductCode",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IdentityContact",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "character varying", nullable: false),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityContacts", x => x.ID);
                    table.ForeignKey(
                        name: "IdentityContact_TypeID",
                        column: x => x.TypeId,
                        principalSchema: "Identity",
                        principalTable: "IdentityContactType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "identitycontact_identitycontactgroup__fk",
                        column: x => x.GroupId,
                        principalSchema: "Identity",
                        principalTable: "IdentityContactGroup",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identitycontacts___fk",
                        column: x => x.CredentialID,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityFavorite",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    FavoriteTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_userfavorites_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_userfavorites_tbl_favoriteType_id_fk",
                        column: x => x.FavoriteTypeID,
                        principalSchema: "Registry",
                        principalTable: "RegistryFavoriteType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userfavorites_tbl_identitycredentials_id_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityRole",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UserCredID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityRoles", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_identityroles_fk",
                        column: x => x.UserCredID,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_identityroles_fk_1",
                        column: x => x.RoleTypeID,
                        principalSchema: "Identity",
                        principalTable: "IdentityRoleType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerification",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    StatusUpdatedOn = table.Column<DateTimeOffset>(type: "time with time zone", nullable: true),
                    Token = table.Column<string>(type: "character varying", nullable: true),
                    Expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityVerifications", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_UserVerifications_AuthID",
                        column: x => x.CredentialID,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_UserVerifications_VerificationTypeID",
                        column: x => x.VerificationTypeID,
                        principalSchema: "Identity",
                        principalTable: "IdentityVerificationType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageDirect",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ParentMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sender = table.Column<string>(type: "character varying", nullable: false),
                    Recipient = table.Column<string>(type: "character varying", nullable: false),
                    Intent = table.Column<string>(type: "character varying", nullable: false),
                    Subject = table.Column<string>(type: "character varying", nullable: false),
                    Message = table.Column<string>(type: "character varying", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagedirect_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagedirect_identitycredential_2_id_fk",
                        column: x => x.RecipientId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "messagedirect_identitycredential_id_fk",
                        column: x => x.SenderId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagedirect_messagedirect_id_fk",
                        column: x => x.ParentMessageId,
                        principalSchema: "Messaging",
                        principalTable: "MessageDirect",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "messagedirect_messagetype_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "SessionData",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    SessionTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionData = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_SessionData", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_sessiondata_fk",
                        column: x => x.SessionTypeID,
                        principalSchema: "Identity",
                        principalTable: "SessionType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_sessiondata_fk_1",
                        column: x => x.CredentialID,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscription",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("subscription_pk", x => x.ID);
                    table.ForeignKey(
                        name: "subscription_identitycredential_id_fk",
                        column: x => x.CredentialID,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "subscription_subscriptionentity_id_fk",
                        column: x => x.TypeID,
                        principalSchema: "Affiliate",
                        principalTable: "SubscriptionType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Wallet",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_Wallets_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_Wallets_CredentialId_fkey",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_Wallets_WalletTypeId_fkey",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThreadMember",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethreadmember_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagethreadmember_identitycredential_id_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagethreadmember_messagethread_id_fk",
                        column: x => x.MessageThreadId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThread",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagethreadmember_messagethreadmembergroup_id_fk",
                        column: x => x.GroupId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThreadMemberGroup",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "AddressBarangay",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CityCodeId = table.Column<long>(type: "bigint", nullable: false),
                    RegCode = table.Column<int>(type: "integer", nullable: true),
                    ProvCode = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("addresses_refbrgy_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_addressbarangay_tbl_addresscity_code_fk",
                        column: x => x.CityCodeId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCity",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityConnection",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    SourceSocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialmediaconnection_pk", x => x.ID);
                    table.ForeignKey(
                        name: "metadata_metadataentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Community",
                        principalTable: "CommunityConnectionType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmedia_sourcesocialmediaidentityid_id_fk",
                        column: x => x.SourceSocialMediaIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmedia_targetsocialmediaidentityid_id_fk",
                        column: x => x.TargetSocialMediaIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "CommunityContent",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "character varying", nullable: true),
                    Text = table.Column<string>(type: "character varying", nullable: true),
                    SocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommunityGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialmediacontent_pk", x => x.ID);
                    table.ForeignKey(
                        name: "communitycontent_communityidentity_id_fk",
                        column: x => x.CommunityGroupId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "socialmediacontent_socialmediacontent_id_fk",
                        column: x => x.ParentContentId,
                        principalSchema: "Community",
                        principalTable: "CommunityContent",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmediacontent_socialmediacontententity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Community",
                        principalTable: "CommunityContentType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmediacontent_socialmediaidentity_id_fk",
                        column: x => x.SocialMediaIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "CommunityIdentityFile",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("communityidentityfiles_pk", x => x.ID);
                    table.ForeignKey(
                        name: "communityidentityfile_communityidentityfileentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentityFileType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "communityidentityfiles_communityidentity_id_fk",
                        column: x => x.IdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "communityidentityfiles_storagefile_id_fk",
                        column: x => x.StorageId,
                        principalSchema: "Storage",
                        principalTable: "StorageFile",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackage",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageStatus = table.Column<short>(type: "smallint", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserDepositRequestID = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActivationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CodeString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConsumedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BusinessPackages_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "BusinessPackage_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "BusinessPackage_TypeId",
                        column: x => x.TypeId,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackageType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "DepositRequestId",
                        column: x => x.UserDepositRequestID,
                        principalSchema: "Wallet",
                        principalTable: "DepositRequest",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userbusinesspackage_fk",
                        column: x => x.ConsumedById,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userbusinesspackage_tbl_userauth_id_fk",
                        column: x => x.RecipientCredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IncomePartition",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IdentityRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_IncomePartition_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "IncomeTypeId",
                        column: x => x.IncomeTypeId,
                        principalSchema: "Income",
                        principalTable: "IncomeType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "UserRoleId",
                        column: x => x.IdentityRoleId,
                        principalSchema: "Identity",
                        principalTable: "IdentityRole",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletAddress",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletAddresses_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "walletaddress_wallet_id_fk",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "WalletTransaction",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RunningBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletTransactions_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "SourceUserWalletId",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "UserAuthID",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequest",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    WithdrawalStatus = table.Column<short>(type: "smallint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WithdrawalRequest_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "WithdrawalRequest_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "WithdrawalRequest_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Message",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Text = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("message_pk", x => x.ID);
                    table.ForeignKey(
                        name: "message_messagethread_id_fk",
                        column: x => x.MessageThreadId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThread",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "message_messagethreadmember_id_fk",
                        column: x => x.MessageThreadMemberId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThreadMember",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThreadMemberRole",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethreadmemberrole_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagethreadmemberrole_identityrole_id_fk",
                        column: x => x.RoleId,
                        principalSchema: "Identity",
                        principalTable: "IdentityRole",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagethreadmemberrole_messagethreadmember_id_fk",
                        column: x => x.MessageThreadMemberId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThreadMember",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IdentityAddress",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityInfoID = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityAddresses", x => x.ID);
                    table.ForeignKey(
                        name: "AddressTypeID",
                        column: x => x.AddressTypeID,
                        principalSchema: "Identity",
                        principalTable: "IdentityAddressType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "UserInfoID",
                        column: x => x.IdentityInfoID,
                        principalSchema: "Identity",
                        principalTable: "IdentityInformation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk",
                        column: x => x.RegionId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressRegion",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_brgy",
                        column: x => x.BarangayId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressBarangay",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_city",
                        column: x => x.CityId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCity",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_province",
                        column: x => x.ProvinceId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressProvince",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_identityaddresses_tbl_addresscountry__fk",
                        column: x => x.CountryId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCountry",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityContentFiles",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialmediacontentfiles_pk", x => x.ID);
                    table.ForeignKey(
                        name: "socialmediacontentfiles_socialmediacontent_id_fk",
                        column: x => x.ContentId,
                        principalSchema: "Community",
                        principalTable: "CommunityContent",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmediacontentfiles_storagefile_id_fk",
                        column: x => x.StorageId,
                        principalSchema: "Storage",
                        principalTable: "StorageFile",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "CommunityContentReaction",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("socialmediacontentreaction_pk", x => x.ID);
                    table.ForeignKey(
                        name: "socialmediacontentreaction_contentreactionentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Community",
                        principalTable: "CommunityContentReactionType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmediacontentreaction_socialmediacontent_id_fk",
                        column: x => x.ContentId,
                        principalSchema: "Community",
                        principalTable: "CommunityContent",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "socialmediacontentreaction_socialmediaidentity_id_fk",
                        column: x => x.SocialMediaIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BinaryMap",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SponsorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UplineUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<short>(type: "smallint", nullable: true),
                    LeftLegCount = table.Column<long>(type: "bigint", nullable: false),
                    RightLegCount = table.Column<long>(type: "bigint", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    BinaryLeft = table.Column<long>(type: "bigint", nullable: true),
                    BinaryRight = table.Column<long>(type: "bigint", nullable: true),
                    LeftLegSilverCount = table.Column<int>(type: "integer", nullable: true),
                    RightLegSilverCount = table.Column<int>(type: "integer", nullable: true),
                    LeftLegGoldCount = table.Column<int>(type: "integer", nullable: true),
                    RightLegGoldCount = table.Column<int>(type: "integer", nullable: true),
                    UplineLevel = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BinaryMap_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "SponsorUserId",
                        column: x => x.SponsorUserId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_usermap_fk",
                        column: x => x.ID,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackage",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "uplineuserbpid",
                        column: x => x.UplineUserId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackageInclusion",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BusinessPackageID = table.Column<Guid>(type: "uuid", nullable: false),
                    InclusionTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(16,5)", precision: 16, scale: 5, nullable: true),
                    StringValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BusinessPackageInclusions_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "BusinessPackageInclusions_BusinessPackageId",
                        column: x => x.BusinessPackageID,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackage",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "BusinessPackageInclusions_TypeID",
                        column: x => x.InclusionTypeID,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackageInclusionsType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackageUpgradeTransaction",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("businesspackageupgradetransactions_pk", x => x.ID);
                    table.ForeignKey(
                        name: "ubput_tbl_businesspackage_id_fk",
                        column: x => x.CurrentBusinessPackageId,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackage",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ubput_tbl_identitycredentials_id_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "CommissionDeductionRequest",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    BusinessPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    DeductionCharge = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_usercommissiondeductionrequest_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_ucdr_tbl_userbusinesspackage_id_fk",
                        column: x => x.BusinessPackageId,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackage",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IncomeDistribution",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastChanged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BusinessPackageID = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: false),
                    DistributionType = table.Column<long>(type: "bigint", nullable: false),
                    MaxLimit = table.Column<long>(type: "bigint", nullable: true),
                    MinLimit = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_IncomeDistribution_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "BusinessPackageID",
                        column: x => x.BusinessPackageID,
                        principalSchema: "BusinessPackage",
                        principalTable: "BusinessPackage",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "IncomeTypeID",
                        column: x => x.IncomeTypeID,
                        principalSchema: "Income",
                        principalTable: "IncomeType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageDelivery",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagedelivery_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagedelivery_message_id_fk",
                        column: x => x.MessageId,
                        principalSchema: "Messaging",
                        principalTable: "Message",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagedelivery_messagedeliveryentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageDeliveryType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagedelivery_messagethreadmember_id_fk",
                        column: x => x.MessageThreadMemberId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThreadMember",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageFiles",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagefiles_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagefiles_message_id_fk",
                        column: x => x.MessageId,
                        principalSchema: "Messaging",
                        principalTable: "Message",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagefiles_storagefile_id_fk",
                        column: x => x.StorageId,
                        principalSchema: "Storage",
                        principalTable: "StorageFile",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageReaction",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagereaction_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagereaction_message_id_fk",
                        column: x => x.MessageId,
                        principalSchema: "Messaging",
                        principalTable: "Message",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagereaction_messagereactionentity_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageReactionType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BinaryList",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    TargetUserMapID = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUserMapID = table.Column<Guid>(type: "uuid", nullable: true),
                    Placement = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_userbinarylist_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_userbinarylist_tbl_usermap_id_fk",
                        column: x => x.TargetUserMapID,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userbinarylist_tbl_usermap_id_fk_2",
                        column: x => x.SourceUserMapID,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "BinaryListMultiplex",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeftCount = table.Column<long>(type: "bigint", nullable: true),
                    RightCount = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    BinaryMapId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_userbinarylistmultiplex_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_userbinarylistmultiplex_tbl_usermap_id_fk",
                        column: x => x.BinaryMapId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "IncomeTransaction",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomeValue = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    TransactionType = table.Column<short>(type: "smallint", nullable: true),
                    SourceMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeStatus = table.Column<short>(type: "smallint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    TargetMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairMapId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_IncomeTransactions_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "IncomeTransaction_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_userincometransaction_tbl_incometype_id_fk",
                        column: x => x.IncomeTypeId,
                        principalSchema: "Income",
                        principalTable: "IncomeType",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userincometransaction_tbl_usermap_id_fk",
                        column: x => x.TargetMapId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_userincometransaction_tbl_usermap_id_fk_2",
                        column: x => x.SourceMapId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_userincometransaction_tbl_usermap_id_fk_3",
                        column: x => x.PairMapId,
                        principalSchema: "Affiliate",
                        principalTable: "BinaryMap",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillsPaymentTransaction",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawRequest = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    RawResponse = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ServiceCharge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ResponseReasonPhrase = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    ResponseHttpStatus = table.Column<int>(type: "integer", nullable: true),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    IncomeTransactionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_userbillspaymenttransaction_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_userbillspaymenttransaction_tbl_billers_id_fk",
                        column: x => x.BillerId,
                        principalSchema: "Integration.BillsPayment",
                        principalTable: "Biller",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_userbillspaymenttransaction_tbl_identitycredentials_id_fk",
                        column: x => x.CredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_userbillspaymenttransaction_tbl_userincometransaction_id_fk",
                        column: x => x.IncomeTransactionId,
                        principalSchema: "Income",
                        principalTable: "IncomeTransaction",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "addresses_refbrgy_code_uindex",
                schema: "GeoLocation",
                table: "AddressBarangay",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressBarangay_CityCodeId",
                schema: "GeoLocation",
                table: "AddressBarangay",
                column: "CityCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressCity_ProvCodeId",
                schema: "GeoLocation",
                table: "AddressCity",
                column: "ProvCodeId");

            migrationBuilder.CreateIndex(
                name: "tbl_addresscity_code_uindex",
                schema: "GeoLocation",
                table: "AddressCity",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressCountry_CurrencyID",
                schema: "GeoLocation",
                table: "AddressCountry",
                column: "CurrencyID");

            migrationBuilder.CreateIndex(
                name: "IX_AddressProvince_RegCodeId",
                schema: "GeoLocation",
                table: "AddressProvince",
                column: "RegCodeId");

            migrationBuilder.CreateIndex(
                name: "tbl_addressprovince_code_uindex",
                schema: "GeoLocation",
                table: "AddressProvince",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressRegion_CountryID",
                schema: "GeoLocation",
                table: "AddressRegion",
                column: "CountryID");

            migrationBuilder.CreateIndex(
                name: "tbl_addressregions_code_uindex",
                schema: "GeoLocation",
                table: "AddressRegion",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Application_EnterpriseID",
                schema: "Application",
                table: "Application",
                column: "EnterpriseID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityAuthorizationLogs_CredentialID",
                schema: "Audit",
                table: "AuthorizationLog",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_Biller_BillerCategoryID",
                schema: "Integration.BillsPayment",
                table: "Biller",
                column: "BillerCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Biller_ProviderEndpointId",
                schema: "Integration.BillsPayment",
                table: "Biller",
                column: "ProviderEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_BillerCategory_ProviderTypeID",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                column: "ProviderTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_BillerField_BillerID",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                column: "BillerID");

            migrationBuilder.CreateIndex(
                name: "IX_BillsPaymentTransaction_BillerId",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                column: "BillerId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsPaymentTransaction_CredentialId",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsPaymentTransaction_IncomeTransactionId",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                column: "IncomeTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BinaryList_SourceUserMapID",
                schema: "Affiliate",
                table: "BinaryList",
                column: "SourceUserMapID");

            migrationBuilder.CreateIndex(
                name: "IX_BinaryList_TargetUserMapID",
                schema: "Affiliate",
                table: "BinaryList",
                column: "TargetUserMapID");

            migrationBuilder.CreateIndex(
                name: "IX_BinaryListMultiplex_BinaryMapId",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                column: "BinaryMapId");

            migrationBuilder.CreateIndex(
                name: "IX_BinaryMap_SponsorUserId",
                schema: "Affiliate",
                table: "BinaryMap",
                column: "SponsorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BinaryMap_UplineUserId",
                schema: "Affiliate",
                table: "BinaryMap",
                column: "UplineUserId");

            migrationBuilder.CreateIndex(
                name: "tbl_usermap_alias_uindex",
                schema: "Affiliate",
                table: "BinaryMap",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackage_ConsumedById",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                column: "ConsumedById");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackage_CredentialId",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackage_RecipientCredentialId",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                column: "RecipientCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackage_TypeId",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackage_UserDepositRequestID",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                column: "UserDepositRequestID");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackageInclusion_BusinessPackageID",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                column: "BusinessPackageID");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackageInclusion_InclusionTypeID",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                column: "InclusionTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackageUpgradeTransaction_CredentialId",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPackageUpgradeTransaction_CurrentBusinessPackageId",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                column: "CurrentBusinessPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionDeductionRequest_BusinessPackageId",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                column: "BusinessPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityConnection_SourceSocialMediaIdentityId",
                schema: "Community",
                table: "CommunityConnection",
                column: "SourceSocialMediaIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityConnection_TargetSocialMediaIdentityId",
                schema: "Community",
                table: "CommunityConnection",
                column: "TargetSocialMediaIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityConnection_TypeId",
                schema: "Community",
                table: "CommunityConnection",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContent_CommunityGroupId",
                schema: "Community",
                table: "CommunityContent",
                column: "CommunityGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContent_ParentContentId",
                schema: "Community",
                table: "CommunityContent",
                column: "ParentContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContent_SocialMediaIdentityId",
                schema: "Community",
                table: "CommunityContent",
                column: "SocialMediaIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContent_TypeId",
                schema: "Community",
                table: "CommunityContent",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentFiles_ContentId",
                schema: "Community",
                table: "CommunityContentFiles",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentFiles_StorageId",
                schema: "Community",
                table: "CommunityContentFiles",
                column: "StorageId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentReaction_ContentId",
                schema: "Community",
                table: "CommunityContentReaction",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentReaction_SocialMediaIdentityId",
                schema: "Community",
                table: "CommunityContentReaction",
                column: "SocialMediaIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentReaction_TypeId",
                schema: "Community",
                table: "CommunityContentReaction",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentity_CredentialId",
                schema: "Community",
                table: "CommunityIdentity",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentity_TypeId",
                schema: "Community",
                table: "CommunityIdentity",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentityFile_IdentityId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "IdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentityFile_StorageId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "StorageId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentityFile_TypeId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_CredentialId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_GatewayId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "GatewayId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_SourceCurrencyId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "SourceCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_WalletTypeId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "WalletTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ELoadTransaction_CredentialId",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_ELoadTransaction_TelcoProductCodeID",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                column: "TelcoProductCodeID");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_SourceCurrencyTypeID",
                schema: "Finance",
                table: "ExchangeRate",
                column: "SourceCurrencyTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRate_TargetCurrencyTypeID",
                schema: "Finance",
                table: "ExchangeRate",
                column: "TargetCurrencyTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Gateway_GatewayCategoryID",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                column: "GatewayCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Gateway_ProviderEndpointId",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                column: "ProviderEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayEndpoint_GatewayID",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                column: "GatewayID");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayInstructions_GatewayId",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                column: "GatewayId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayNumber_TelcoTypeID",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                column: "TelcoTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayResponse_GatewayResponseTypeId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                column: "GatewayResponseTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayResponse_ResponseStatusTypeId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                column: "ResponseStatusTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayResponseType_GatewayTypeId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                column: "GatewayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAddress_BarangayId",
                schema: "Identity",
                table: "IdentityAddress",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAddress_CityId",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAddress_CountryId",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAddress_ProvinceId",
                schema: "Identity",
                table: "IdentityAddress",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAddress_RegionId",
                schema: "Identity",
                table: "IdentityAddress",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityAddresses_AddressTypeID",
                schema: "Identity",
                table: "IdentityAddress",
                column: "AddressTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityAddresses_UserInfoID",
                schema: "Identity",
                table: "IdentityAddress",
                column: "IdentityInfoID");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityContact_GroupId",
                schema: "Identity",
                table: "IdentityContact",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityContacts_TypeID",
                schema: "Identity",
                table: "IdentityContact",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "tbl_identitycontacts_CredentialID_index",
                schema: "Identity",
                table: "IdentityContact",
                column: "CredentialID");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityCredential_ApplicationID",
                schema: "Identity",
                table: "IdentityCredential",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityCredentials_IdentityInfoID",
                schema: "Identity",
                table: "IdentityCredential",
                column: "IdentityInfoID");

            migrationBuilder.CreateIndex(
                name: "tbl_identitycredentials_un",
                schema: "Identity",
                table: "IdentityCredential",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityFavorite_CredentialId",
                schema: "Identity",
                table: "IdentityFavorite",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityFavorite_FavoriteTypeID",
                schema: "Identity",
                table: "IdentityFavorite",
                column: "FavoriteTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityInformation_ApplicationID",
                schema: "Identity",
                table: "IdentityInformation",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityRoles_RoleTypeID",
                schema: "Identity",
                table: "IdentityRole",
                column: "RoleTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityRoles_UserCredID",
                schema: "Identity",
                table: "IdentityRole",
                column: "UserCredID");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleType_ApplicationId",
                schema: "Identity",
                table: "IdentityRoleType",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleType_GroupId",
                schema: "Identity",
                table: "IdentityRoleType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityVerifications_CredentialID",
                schema: "Identity",
                table: "IdentityVerification",
                column: "CredentialID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityVerifications_VerificationTypeID",
                schema: "Identity",
                table: "IdentityVerification",
                column: "VerificationTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeDistribution_BusinessPackageID",
                schema: "Income",
                table: "IncomeDistribution",
                column: "BusinessPackageID");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeDistribution_IncomeTypeID",
                schema: "Income",
                table: "IncomeDistribution",
                column: "IncomeTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_IncomePartition_IdentityRoleId",
                schema: "Income",
                table: "IncomePartition",
                column: "IdentityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomePartition_IncomeTypeId",
                schema: "Income",
                table: "IncomePartition",
                column: "IncomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_CredentialId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_IncomeTypeId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "IncomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_PairMapId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "PairMapId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_SourceMapId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "SourceMapId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_TargetMapId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "TargetMapId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ApplicationLogs_AppID",
                schema: "Audit",
                table: "Log",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Message_MessageThreadId",
                schema: "Messaging",
                table: "Message",
                column: "MessageThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_MessageThreadMemberId",
                schema: "Messaging",
                table: "Message",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_MessageId",
                schema: "Messaging",
                table: "MessageDelivery",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageDelivery",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_TypeId",
                schema: "Messaging",
                table: "MessageDelivery",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDirect_ParentMessageId",
                schema: "Messaging",
                table: "MessageDirect",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDirect_RecipientId",
                schema: "Messaging",
                table: "MessageDirect",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDirect_SenderId",
                schema: "Messaging",
                table: "MessageDirect",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDirect_TypeId",
                schema: "Messaging",
                table: "MessageDirect",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFiles_MessageId",
                schema: "Messaging",
                table: "MessageFiles",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFiles_StorageId",
                schema: "Messaging",
                table: "MessageFiles",
                column: "StorageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_MessageId",
                schema: "Messaging",
                table: "MessageReaction",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_TypeId",
                schema: "Messaging",
                table: "MessageReaction",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThread_TypeId",
                schema: "Messaging",
                table: "MessageThread",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_CredentialId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_GroupId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_MessageThreadId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMemberGroup_MessageThreadId",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                column: "MessageThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMemberRole_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMemberRole_RoleId",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadType_MessageTypeId",
                schema: "Messaging",
                table: "MessageThreadType",
                column: "MessageTypeId");

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
                name: "IX_ProviderEndpoint_ProviderID",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                column: "ProviderID");

            migrationBuilder.CreateIndex(
                name: "IX_RegistryConfiguration_ApplicationID",
                schema: "Registry",
                table: "RegistryConfiguration",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_RegistryConfiguration_GroupId",
                schema: "Registry",
                table: "RegistryConfiguration",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_SessionData_CredentialID",
                schema: "Identity",
                table: "SessionData",
                column: "CredentialID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_SessionData_SessionTypeID",
                schema: "Identity",
                table: "SessionData",
                column: "SessionTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFile_StorageFileIdentifierId",
                schema: "Storage",
                table: "StorageFile",
                column: "StorageFileIdentifierId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFile_TypeId",
                schema: "Storage",
                table: "StorageFile",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFileIdentifier_GroupId",
                schema: "Storage",
                table: "StorageFileIdentifier",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_CredentialID",
                schema: "Affiliate",
                table: "Subscription",
                column: "CredentialID");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_TypeID",
                schema: "Affiliate",
                table: "Subscription",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_TelcoPrefix_TelcoTypeID",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                column: "TelcoTypeID");

            migrationBuilder.CreateIndex(
                name: "tbl_telcoprefix_prefix_index",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_TelcoProductCode_TelcoCodePromosID",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                column: "TelcoCodePromosID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_TelcoProductCode_TelcoTypeID",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                column: "TelcoTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_CredentialId",
                schema: "Wallet",
                table: "Wallet",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_WalletTypeId",
                schema: "Wallet",
                table: "Wallet",
                column: "WalletTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletAddress_WalletId",
                schema: "Wallet",
                table: "WalletAddress",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_CredentialId",
                schema: "Wallet",
                table: "WalletTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_WalletId",
                schema: "Wallet",
                table: "WalletTransaction",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletType_ApplicationId",
                schema: "Wallet",
                table: "WalletType",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletType_CurrencyTypeID",
                schema: "Wallet",
                table: "WalletType",
                column: "CurrencyTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_CredentialId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_WalletId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_WalletTypeId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthorizationLog",
                schema: "Audit");

            migrationBuilder.DropTable(
                name: "BillerField",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "BillsPaymentTransaction",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "BinaryList",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "BinaryListMultiplex",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "BusinessPackageInclusion",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "BusinessPackageUpgradeTransaction",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "CommissionDeductionRequest",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "CommunityConnection",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContentFiles",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContentReaction",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityIdentityFile",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "ELoadTransaction",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "ExchangeRate",
                schema: "Finance");

            migrationBuilder.DropTable(
                name: "GatewayInstructions",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "GatewayNumber",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "GatewayResponse",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "IdentityAddress",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IdentityContact",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IdentityFavorite",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IdentityVerification",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IncomeDistribution",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "IncomePartition",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "Log",
                schema: "Audit");

            migrationBuilder.DropTable(
                name: "MessageDelivery",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageDirect",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageFiles",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageReaction",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageThreadMemberRole",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MetaData",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "RegistryConfiguration",
                schema: "Registry");

            migrationBuilder.DropTable(
                name: "SessionData",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "Subscription",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "TelcoPrefix",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "WalletAddress",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletTransaction",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WithdrawalRequest",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "Biller",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "IncomeTransaction",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "BusinessPackageInclusionsType",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "CommunityConnectionType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContentReactionType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContent",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityIdentityFileType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "TelcoProductCode",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "GatewayResponseStatusType",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "GatewayResponseType",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "IdentityAddressType",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "AddressBarangay",
                schema: "GeoLocation");

            migrationBuilder.DropTable(
                name: "IdentityContactType",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IdentityContactGroup",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "RegistryFavoriteType",
                schema: "Registry");

            migrationBuilder.DropTable(
                name: "IdentityVerificationType",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "MessageDeliveryType",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "StorageFile",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "Message",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageReactionType",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "IdentityRole",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "MetaDataType",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "RegistryConfigurationGroup",
                schema: "Registry");

            migrationBuilder.DropTable(
                name: "SessionType",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "SubscriptionType",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "Wallet",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "BillerCategory",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "ProviderEndpoint",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "IncomeType",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "BinaryMap",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "CommunityIdentity",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContentType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "TelcoType",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "TelcoCodePromo",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "AddressCity",
                schema: "GeoLocation");

            migrationBuilder.DropTable(
                name: "StorageFileType",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "StorageFileIdentifier",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "MessageThreadMember",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "IdentityRoleType",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "MetaDataEntityGroup",
                schema: "MetaData");

            migrationBuilder.DropTable(
                name: "ProviderType",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "BusinessPackage",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "CommunityIdentityType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "AddressProvince",
                schema: "GeoLocation");

            migrationBuilder.DropTable(
                name: "StorageFileIdentifierGroup",
                schema: "Storage");

            migrationBuilder.DropTable(
                name: "MessageThreadMemberGroup",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "IdentityRoleEntityGroup",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "BusinessPackageType",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "DepositRequest",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "AddressRegion",
                schema: "GeoLocation");

            migrationBuilder.DropTable(
                name: "MessageThread",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "IdentityCredential",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "Gateway",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "WalletType",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "AddressCountry",
                schema: "GeoLocation");

            migrationBuilder.DropTable(
                name: "MessageThreadType",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "IdentityInformation",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "GatewayCategory",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "GatewayEndpoint",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "CurrencyType",
                schema: "Finance");

            migrationBuilder.DropTable(
                name: "MessageType",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "Application",
                schema: "Application");

            migrationBuilder.DropTable(
                name: "GatewayType",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "Enterprise",
                schema: "Application");
        }
    }
}
