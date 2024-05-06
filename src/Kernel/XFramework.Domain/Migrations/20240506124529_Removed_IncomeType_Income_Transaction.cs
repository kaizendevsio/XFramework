using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Removed_IncomeType_Income_Transaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "IncomeTransaction",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "BusinessPackageInclusionsType",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "BusinessPackage",
                schema: "BusinessPackage");

            migrationBuilder.DropTable(
                name: "IncomeType",
                schema: "Income");

            migrationBuilder.DropTable(
                name: "BusinessPackageType",
                schema: "BusinessPackage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BusinessPackage");

            migrationBuilder.EnsureSchema(
                name: "Income");

            migrationBuilder.CreateTable(
                name: "BusinessPackageInclusionsType",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IconImage = table.Column<string>(type: "character varying", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsNumericValue = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_BusinessPackageType_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IncomeType",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IncomeTypeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IncomeTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IncomeTypeShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsReward = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_IncomeType_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackage",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ConsumedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserDepositRequestID = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CodeHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CodeString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    PackageStatus = table.Column<short>(type: "smallint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IncomeTransaction",
                schema: "Income",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IncomeStatus = table.Column<short>(type: "smallint", nullable: true),
                    IncomeValue = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    PairMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SourceMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<short>(type: "smallint", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "BusinessPackageInclusion",
                schema: "BusinessPackage",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BusinessPackageID = table.Column<Guid>(type: "uuid", nullable: false),
                    InclusionTypeID = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StringValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(16,5)", precision: 16, scale: 5, nullable: true)
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
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    PreviousBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserBusinessPackageId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    BusinessPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeductionCharge = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IX_IncomeTransaction_CredentialId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeTransaction_IncomeTypeId",
                schema: "Income",
                table: "IncomeTransaction",
                column: "IncomeTypeId");
        }
    }
}
