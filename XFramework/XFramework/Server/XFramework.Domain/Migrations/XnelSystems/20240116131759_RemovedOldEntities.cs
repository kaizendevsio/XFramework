using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class RemovedOldEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk_2",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk_3",
                schema: "Income",
                table: "IncomeTransaction");

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
                name: "ELoadTransaction",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "GatewayNumber",
                schema: "Integration.ELoad");

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
                name: "SessionData",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "TelcoPrefix",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "Biller",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "BinaryMap",
                schema: "Affiliate");

            migrationBuilder.DropTable(
                name: "TelcoProductCode",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "BillerCategory",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "ProviderEndpoint",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropTable(
                name: "TelcoType",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "TelcoCodePromo",
                schema: "Integration.ELoad");

            migrationBuilder.DropTable(
                name: "ProviderType",
                schema: "Integration.BillsPayment");

            migrationBuilder.DropIndex(
                name: "IX_IncomeTransaction_PairMapId",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropIndex(
                name: "IX_IncomeTransaction_SourceMapId",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropIndex(
                name: "IX_IncomeTransaction_TargetMapId",
                schema: "Income",
                table: "IncomeTransaction");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Session",
                schema: "Identity");

            migrationBuilder.EnsureSchema(
                name: "Integration.BillsPayment");

            migrationBuilder.EnsureSchema(
                name: "Integration.ELoad");

            migrationBuilder.CreateTable(
                name: "BinaryMap",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UplineUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Alias = table.Column<string>(type: "character varying", nullable: true),
                    BinaryLeft = table.Column<long>(type: "bigint", nullable: true),
                    BinaryRight = table.Column<long>(type: "bigint", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LeftLegCount = table.Column<long>(type: "bigint", nullable: false),
                    LeftLegGoldCount = table.Column<int>(type: "integer", nullable: true),
                    LeftLegSilverCount = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    Position = table.Column<short>(type: "smallint", nullable: true),
                    RightLegCount = table.Column<long>(type: "bigint", nullable: false),
                    RightLegGoldCount = table.Column<int>(type: "integer", nullable: true),
                    RightLegSilverCount = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "IncomeDistribution",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BusinessPackageID = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DistributionType = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLimit = table.Column<long>(type: "bigint", nullable: true),
                    MinLimit = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: false)
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
                name: "IncomePartition",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IdentityRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    Percentage = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "Log",
                schema: "Audit",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Initiator = table.Column<string>(type: "character varying", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    Seen = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    Severity = table.Column<short>(type: "smallint", nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ApplicationLogs", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_applogs_appid_fk",
                        column: x => x.ApplicationId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderType",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_providerType_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SessionData",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CredentialID = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionData = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                name: "TelcoCodePromo",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_TelcoType", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BinaryList",
                schema: "Affiliate",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SourceUserMapID = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUserMapID = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    Placement = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    BinaryMapId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LeftCount = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    RightCount = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "BillerCategory",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProviderTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    BaseUrlEndpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "GatewayNumber",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    TelcoTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gateway = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Prefix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    TelcoCodePromosID = table.Column<Guid>(type: "uuid", nullable: true),
                    TelcoTypeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Code = table.Column<string>(type: "character varying", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Validity = table.Column<string>(type: "character varying", nullable: true)
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
                name: "Biller",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BillerCategoryID = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    Image = table.Column<string>(type: "character varying", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    ServiceCharge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "ELoadTransaction",
                schema: "Integration.ELoad",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelcoProductCodeID = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<string>(type: "character varying", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CurrentBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    CustomerNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    PreviousBalance = table.Column<decimal>(type: "numeric", nullable: true),
                    RawRequest = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    RawResponse = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SenderNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionID = table.Column<string>(type: "character varying", nullable: true),
                    WalletTypeID = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "BillerField",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BillerID = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    isEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying", nullable: false),
                    Width = table.Column<decimal>(type: "numeric(2,0)", precision: 2, nullable: true)
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
                name: "BillsPaymentTransaction",
                schema: "Integration.BillsPayment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BillerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    ConvenienceFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, defaultValueSql: "0"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    RawRequest = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    RawResponse = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponseHttpStatus = table.Column<int>(type: "integer", nullable: true),
                    ResponseReasonPhrase = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    ServiceCharge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
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
                name: "IX_GatewayNumber_TelcoTypeID",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                column: "TelcoTypeID");

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
                name: "IX_tbl_ApplicationLogs_AppID",
                schema: "Audit",
                table: "Log",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderEndpoint_ProviderID",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                column: "ProviderID");

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

            migrationBuilder.AddForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk",
                schema: "Income",
                table: "IncomeTransaction",
                column: "TargetMapId",
                principalSchema: "Affiliate",
                principalTable: "BinaryMap",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk_2",
                schema: "Income",
                table: "IncomeTransaction",
                column: "SourceMapId",
                principalSchema: "Affiliate",
                principalTable: "BinaryMap",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_userincometransaction_tbl_usermap_id_fk_3",
                schema: "Income",
                table: "IncomeTransaction",
                column: "PairMapId",
                principalSchema: "Affiliate",
                principalTable: "BinaryMap",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
