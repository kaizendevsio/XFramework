using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
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
                name: "Community");

            migrationBuilder.EnsureSchema(
                name: "Finance");

            migrationBuilder.EnsureSchema(
                name: "Wallet");

            migrationBuilder.EnsureSchema(
                name: "Integration.PaymentGateway");

            migrationBuilder.EnsureSchema(
                name: "Identity");

            migrationBuilder.EnsureSchema(
                name: "Messaging");

            migrationBuilder.EnsureSchema(
                name: "MetaData");

            migrationBuilder.EnsureSchema(
                name: "Registry");

            migrationBuilder.EnsureSchema(
                name: "Storage");

            migrationBuilder.EnsureSchema(
                name: "Affiliate");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "Application",
                schema: "Application",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    Expiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailabilityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentAppID = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Application", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CommunityConnectionType",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrencyIsoCode3 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_currency_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCategory",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultExpiry = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<short>(type: "smallint", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_VerificationType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageDeliveryType",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("metadataentitygroup_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RegistryConfigurationGroup",
                schema: "Registry",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("subscriptionentity_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IdentityInformation",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Suffix = table.Column<string>(type: "text", nullable: true),
                    IdentityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdentityDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CivilStatus = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityInfo", x => x.ID);
                    table.ForeignKey(
                        name: "identityinformation_application_id_fk",
                        column: x => x.TenantId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "AddressCountry",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IsoCode2 = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsoCode3 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhoneCountryCode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    CurrencyID = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    SourceCurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    EffectivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "WalletType",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Desc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    CurrencyTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    MinTransferRule = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxTransferRule = table.Column<decimal>(type: "numeric", nullable: true),
                    BondBalanceRule = table.Column<decimal>(type: "numeric", nullable: true),
                    MaintainingBalanceRule = table.Column<decimal>(type: "numeric", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                        column: x => x.TenantId,
                        principalSchema: "Application",
                        principalTable: "Application",
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
                    UrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: false),
                    GatewayTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IdentityRoleType",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoleLevel = table.Column<short>(type: "smallint", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                        column: x => x.TenantId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageThreadType",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    MessageTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "RegistryConfiguration",
                schema: "Registry",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Key = table.Column<string>(type: "character varying", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_applicationconfiguration_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_applicationconfiguration_tbl_application_id_fk",
                        column: x => x.TenantId,
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
                name: "StorageFileIdentifier",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IdentityCredential",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IdentityInfoID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAlias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LogInStatus = table.Column<short>(type: "smallint", nullable: true),
                    PasswordByte = table.Column<byte[]>(type: "bytea", nullable: true),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnlineSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusMessage = table.Column<string>(type: "text", nullable: true),
                    LastActivityType = table.Column<string>(type: "text", nullable: true),
                    Device = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_IdentityCredentials", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_identitycredentials___fk",
                        column: x => x.TenantId,
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
                name: "AddressRegion",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PsgcCode = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<long>(type: "bigint", nullable: false),
                    CountryID = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ProviderEndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Code = table.Column<string>(type: "character varying", nullable: false),
                    Message = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    ResponseStatusTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayResponseTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "StorageFile",
                schema: "Storage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ContentPath = table.Column<string>(type: "character varying", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<Guid>(type: "uuid", nullable: false),
                    FileSize = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StorageFileIdentifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    ContentType = table.Column<string>(type: "character varying", nullable: true),
                    BlobContainer = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "AuthorizationLog",
                schema: "Audit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IPAddress = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: true),
                    AuthStatus = table.Column<int>(type: "integer", nullable: true),
                    LoginSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeviceAgent = table.Column<string>(type: "text", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandleName = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastActive = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: true),
                    Tagline = table.Column<string>(type: "character varying", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IdentityContact",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "character varying", nullable: false),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    UserCredID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    StatusUpdatedOn = table.Column<DateTimeOffset>(type: "time with time zone", nullable: true),
                    Token = table.Column<string>(type: "character varying", nullable: true),
                    Expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ParentMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageTransportType = table.Column<int>(type: "integer", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalRecipient = table.Column<string>(type: "text", nullable: true),
                    ExternalSender = table.Column<string>(type: "text", nullable: true),
                    Intent = table.Column<string>(type: "character varying", nullable: true),
                    Subject = table.Column<string>(type: "character varying", nullable: true),
                    Message = table.Column<string>(type: "character varying", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AgentClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagedirect_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagedirect_identitycredential_2_id_fk",
                        column: x => x.RecipientId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
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
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "messagedirect_messagetype_id_fk",
                        column: x => x.TypeId,
                        principalSchema: "Messaging",
                        principalTable: "MessageType",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Session",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SessionTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionData = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    TypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: true),
                    ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    AccountNumber = table.Column<string>(type: "text", nullable: true),
                    CardNumber = table.Column<int>(type: "integer", nullable: false),
                    DebitOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreditOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    TransferableBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    MinTransferRule = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxTransferRule = table.Column<decimal>(type: "numeric", nullable: true),
                    BondBalanceRule = table.Column<decimal>(type: "numeric", nullable: true),
                    MaintainingBalanceRule = table.Column<decimal>(type: "numeric", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "AddressProvince",
                schema: "GeoLocation",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PsgcCode = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RegCodeId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "DepositRequest",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
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
                    GatewayId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "GatewayInstructions",
                schema: "Integration.PaymentGateway",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstructionText = table.Column<string>(type: "character varying", nullable: true),
                    ExampleText = table.Column<string>(type: "character varying", nullable: true),
                    StepOrder = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "CommunityConnection",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SourceSocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Title = table.Column<string>(type: "character varying", nullable: true),
                    Text = table.Column<string>(type: "character varying", nullable: true),
                    SocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommunityGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    IdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "WalletAddress",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Held = table.Column<bool>(type: "boolean", nullable: false),
                    Released = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    TransactionFee = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalFees = table.Column<decimal>(type: "numeric", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    RunningTotalBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    RunningAvailableBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    RunningBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    RunningDebitOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    RunningCreditOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    PreviousTotalBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    PreviousDebitOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    PreviousCreditOnHoldBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Fee = table.Column<decimal>(type: "numeric", nullable: true),
                    WithdrawalStatus = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WithdrawalRequest_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequest_WalletType_WalletTypeId",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID");
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
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "MessageThreadMember",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying", nullable: false),
                    Alias = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "CommunityContentFiles",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocialMediaIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "WalletTransfer",
                schema: "Wallet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionPurpose = table.Column<int>(type: "integer", nullable: false),
                    SenderTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "numeric", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransfer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransfer_WalletTransaction_RecipientTransactionId",
                        column: x => x.RecipientTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletTransfer_WalletTransaction_SenderTransactionId",
                        column: x => x.SenderTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
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
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "Message",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Text = table.Column<string>(type: "character varying", nullable: false),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "WalletTransactionLineItem",
                schema: "Wallet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Fee = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    WalletTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactionLineItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactionLineItem_WalletTransaction_WalletTransacti~",
                        column: x => x.WalletTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_WalletTransactionLineItem_WalletTransfer_WalletTransferId",
                        column: x => x.WalletTransferId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransfer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityAddress",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IdentityInfoID = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    BarangayId = table.Column<Guid>(type: "uuid", nullable: true),
                    CityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subdivision = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddressTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAddress = table.Column<bool>(type: "boolean", nullable: true),
                    ProvinceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsolidatedName = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_brgy",
                        column: x => x.BarangayId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressBarangay",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_city",
                        column: x => x.CityId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCity",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identityaddresses__id_fk_province",
                        column: x => x.ProvinceId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressProvince",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "tbl_identityaddresses_tbl_addresscountry__fk",
                        column: x => x.CountryId,
                        principalSchema: "GeoLocation",
                        principalTable: "AddressCountry",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageDelivery",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityAddressType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "SystemReferenceId", "TenantId" },
                values: new object[,]
                {
                    { new Guid("23c13259-1e24-427d-ba89-a4d2506c7464"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("337ee33d-445f-4e6e-bc61-8709170b0ee4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "SHIPPING", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4eec62eb-08ef-406c-9ea2-2ac2d6e0f206"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BILLING", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("54ab2c38-be75-4572-916b-72019d676162"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("66c8ab89-f24d-4aea-af1a-9ac6a8263575"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c9136227-f5dc-4147-984d-70aa855090e4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "HOME", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityContactGroup",
                columns: new[] { "ID", "ConcurrencyStamp", "DeletedAt", "IsDeleted", "IsEnabled", "Name", "SystemReferenceId", "TenantId" },
                values: new object[,]
                {
                    { new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "WORK", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "HOME", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"), new Guid("00000000-0000-0000-0000-000000000000"), null, false, true, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityContactType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "SystemReferenceId", "TenantId" },
                values: new object[,]
                {
                    { new Guid("03f26cc1-e4c2-424f-9d5b-b22d006ae45b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Email", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("17583df0-c1b2-47a7-875b-2d9b44f55249"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Twitter", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2fa27f70-d083-4327-b04e-74e1295cb4be"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "LinkedIn", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4e5edd0d-5c16-4955-9323-3c6e86b54f0b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Instagram", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cdc88887-c7e7-415e-9d43-cc0050d523d3"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Phone", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d89c4b4a-2077-44ea-958e-4327d191a14c"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Facebook", new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityVerificationType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DefaultExpiry", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "Priority", "SystemReferenceId", "TenantId" },
                values: new object[,]
                {
                    { new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1051200L, null, false, false, null, "KYC", null, new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 10L, null, false, false, null, "SMS", null, new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 120L, null, false, false, null, "Email", null, new Guid("00000000-0000-0000-0000-000000000000"), new Guid("00000000-0000-0000-0000-000000000000") }
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
                name: "IX_tbl_IdentityAuthorizationLogs_CredentialID",
                schema: "Audit",
                table: "AuthorizationLog",
                column: "CredentialId");

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
                name: "IX_IdentityCredential_TenantId",
                schema: "Identity",
                table: "IdentityCredential",
                column: "TenantId");

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
                name: "IX_IdentityInformation_TenantId",
                schema: "Identity",
                table: "IdentityInformation",
                column: "TenantId");

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
                name: "IX_IdentityRoleType_GroupId",
                schema: "Identity",
                table: "IdentityRoleType",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleType_TenantId",
                schema: "Identity",
                table: "IdentityRoleType",
                column: "TenantId");

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
                name: "IX_RegistryConfiguration_GroupId",
                schema: "Registry",
                table: "RegistryConfiguration",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistryConfiguration_TenantId",
                schema: "Registry",
                table: "RegistryConfiguration",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_SessionData_CredentialID",
                schema: "Identity",
                table: "Session",
                column: "CredentialID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_SessionData_SessionTypeID",
                schema: "Identity",
                table: "Session",
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
                name: "IX_WalletTransactionLineItem_WalletTransactionId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactionLineItem_WalletTransferId",
                schema: "Wallet",
                table: "WalletTransactionLineItem",
                column: "WalletTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransfer_RecipientTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                column: "RecipientTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransfer_SenderTransactionId",
                schema: "Wallet",
                table: "WalletTransfer",
                column: "SenderTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletType_CurrencyTypeID",
                schema: "Wallet",
                table: "WalletType",
                column: "CurrencyTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_WalletType_TenantId",
                schema: "Wallet",
                table: "WalletType",
                column: "TenantId");

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
                name: "DepositRequest",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "ExchangeRate",
                schema: "Finance");

            migrationBuilder.DropTable(
                name: "GatewayInstructions",
                schema: "Integration.PaymentGateway");

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
                name: "Session",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "Subscription",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "WalletAddress",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletTransactionLineItem",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WithdrawalRequest",
                schema: "Wallet");

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
                name: "Gateway",
                schema: "Integration.PaymentGateway");

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
                name: "WalletTransfer",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "CommunityIdentity",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "CommunityContentType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "GatewayCategory",
                schema: "Integration.PaymentGateway");

            migrationBuilder.DropTable(
                name: "GatewayEndpoint",
                schema: "Integration.PaymentGateway");

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
                name: "WalletTransaction",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "CommunityIdentityType",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "GatewayType",
                schema: "Integration.PaymentGateway");

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
                name: "Wallet",
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
                name: "CurrencyType",
                schema: "Finance");

            migrationBuilder.DropTable(
                name: "MessageType",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "Application",
                schema: "Application");
        }
    }
}
