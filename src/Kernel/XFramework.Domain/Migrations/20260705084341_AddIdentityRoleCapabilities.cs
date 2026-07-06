using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityRoleCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityRoleFeaturePermissionOverride",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    IdentityRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false),
                    SubFeatureKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false, defaultValue: ""),
                    CapabilityKey = table.Column<string>(type: "character varying", maxLength: 64, nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                    table.PrimaryKey("identityrolefeaturepermissionoverride_pk", x => x.ID);
                    table.ForeignKey(
                        name: "identityrolefeaturepermissionoverride_identityrole_fk",
                        column: x => x.IdentityRoleId,
                        principalSchema: "Identity",
                        principalTable: "IdentityRole",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityRoleTypeFeaturePermission",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    RoleTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false),
                    SubFeatureKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false, defaultValue: ""),
                    CapabilityKey = table.Column<string>(type: "character varying", maxLength: 64, nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                    table.PrimaryKey("identityroletypefeaturepermission_pk", x => x.ID);
                    table.ForeignKey(
                        name: "identityroletypefeaturepermission_roletype_fk",
                        column: x => x.RoleTypeId,
                        principalSchema: "Identity",
                        principalTable: "IdentityRoleType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantAuthorizationPolicy",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MissingPermissionBehavior = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("tenantauthorizationpolicy_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tenantauthorizationpolicy_tenant_tenantid_fk",
                        column: x => x.TenantId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleFeaturePermissionOverride_IdentityRoleId",
                schema: "Identity",
                table: "IdentityRoleFeaturePermissionOverride",
                column: "IdentityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleFeaturePermissionOverride_Role_Feature_Capability",
                schema: "Identity",
                table: "IdentityRoleFeaturePermissionOverride",
                columns: new[] { "TenantId", "IdentityRoleId", "ModuleKey", "SubFeatureKey", "CapabilityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleTypeFeaturePermission_Role_Feature_Capability",
                schema: "Identity",
                table: "IdentityRoleTypeFeaturePermission",
                columns: new[] { "TenantId", "RoleTypeId", "ModuleKey", "SubFeatureKey", "CapabilityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRoleTypeFeaturePermission_RoleTypeId",
                schema: "Identity",
                table: "IdentityRoleTypeFeaturePermission",
                column: "RoleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAuthorizationPolicy_Tenant",
                schema: "Identity",
                table: "TenantAuthorizationPolicy",
                column: "TenantId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "Identity"."TenantAuthorizationPolicy"
                    ("ID", "TenantId", "MissingPermissionBehavior", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt")
                SELECT
                    uuid_generate_v4(),
                    tenant."ID",
                    0,
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now()
                FROM "Application"."Application" tenant
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Identity"."TenantAuthorizationPolicy" policy
                    WHERE policy."TenantId" = tenant."ID"
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Identity"."TenantModuleFeature"
                    ("ID", "ModuleKey", "SubFeatureKey", "DisplayName", "Description", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt", "TenantId")
                SELECT
                    uuid_generate_v4(),
                    definitions."ModuleKey",
                    definitions."SubFeatureKey",
                    definitions."DisplayName",
                    definitions."Description",
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now(),
                    tenant."ID"
                FROM "Application"."Application" tenant
                CROSS JOIN (VALUES
                    ('identity', '', 'Identity', 'Identity, credential, role, tenant, session, and verification administration.'),
                    ('identity', 'users', 'Identity Users', 'Identity information and user administration.'),
                    ('identity', 'credentials', 'Identity Credentials', 'Credential creation, status, avatars, and credential metadata.'),
                    ('identity', 'roles', 'Identity Roles', 'Role assignment, role types, and role capability permissions.'),
                    ('identity', 'tenants', 'Identity Tenants', 'Tenant records, hierarchy, module access, and authorization policy.'),
                    ('identity', 'sessions', 'Identity Sessions', 'Session monitoring, refresh, and termination.'),
                    ('identity', 'verifications', 'Identity Verifications', 'Verification tokens, password reset, and approval workflows.'),
                    ('identity', 'contacts', 'Identity Contacts', 'Credential contact records and contact grouping.'),
                    ('identity', 'addresses', 'Identity Addresses', 'Identity address records and geographic lookups.'),
                    ('identity', 'auth_logs', 'Identity Auth Logs', 'Authentication audit log review.')
                ) AS definitions("ModuleKey", "SubFeatureKey", "DisplayName", "Description")
                WHERE tenant."IsDeleted" = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = tenant."ID"
                        AND existing."ModuleKey" = definitions."ModuleKey"
                        AND existing."SubFeatureKey" = definitions."SubFeatureKey"
                  );
                """);

            migrationBuilder.Sql("""
                WITH features("ModuleKey", "SubFeatureKey") AS (
                    VALUES
                        ('wallets', ''),
                        ('wallets', 'transfers'),
                        ('wallets', 'deposits'),
                        ('wallets', 'withdrawals'),
                        ('wallets', 'batch'),
                        ('wallets', 'reconciliation'),
                        ('wallets', 'policy'),
                        ('wallets', 'webhooks'),
                        ('wallets', 'reporting'),
                        ('inventario', ''),
                        ('inventario', 'catalog'),
                        ('inventario', 'variations'),
                        ('inventario', 'transactions'),
                        ('inventario', 'low_stock_alerts'),
                        ('inventario', 'warehousing'),
                        ('inventario', 'stock_balances'),
                        ('inventario', 'movements'),
                        ('inventario', 'reservations'),
                        ('inventario', 'fulfillment'),
                        ('inventario', 'purchasing'),
                        ('inventario', 'traceability'),
                        ('inventario', 'planning'),
                        ('inventario', 'reporting'),
                        ('inventario', 'negative_stock'),
                        ('pos', ''),
                        ('pos', 'registers'),
                        ('pos', 'sales'),
                        ('pos', 'carts'),
                        ('pos', 'returns'),
                        ('pos', 'reporting'),
                        ('communications', ''),
                        ('communications', 'chat'),
                        ('communications', 'audio_video'),
                        ('community', ''),
                        ('payments', ''),
                        ('notifications', ''),
                        ('attendance', ''),
                        ('storage', ''),
                        ('identity', ''),
                        ('identity', 'users'),
                        ('identity', 'credentials'),
                        ('identity', 'roles'),
                        ('identity', 'tenants'),
                        ('identity', 'sessions'),
                        ('identity', 'verifications'),
                        ('identity', 'contacts'),
                        ('identity', 'addresses'),
                        ('identity', 'auth_logs')
                ),
                capabilities("CapabilityKey") AS (
                    VALUES ('view'), ('create'), ('update'), ('delete'), ('manage')
                )
                UPDATE "Identity"."IdentityRoleTypeFeaturePermission" permission
                SET
                    "Effect" = 1,
                    "IsEnabled" = true,
                    "IsDeleted" = false,
                    "DeletedAt" = NULL,
                    "ModifiedAt" = now()
                FROM "Identity"."IdentityRoleType" role_type
                CROSS JOIN features
                CROSS JOIN capabilities
                WHERE role_type."ID" = permission."RoleTypeId"
                  AND role_type."SystemReferenceId" = '6e7b6bf5-6ad6-49fb-80b0-38e967fc35f3'
                  AND permission."TenantId" = role_type."TenantId"
                  AND permission."ModuleKey" = features."ModuleKey"
                  AND permission."SubFeatureKey" = features."SubFeatureKey"
                  AND permission."CapabilityKey" = capabilities."CapabilityKey";
                """);

            migrationBuilder.Sql("""
                WITH features("ModuleKey", "SubFeatureKey") AS (
                    VALUES
                        ('wallets', ''),
                        ('wallets', 'transfers'),
                        ('wallets', 'deposits'),
                        ('wallets', 'withdrawals'),
                        ('wallets', 'batch'),
                        ('wallets', 'reconciliation'),
                        ('wallets', 'policy'),
                        ('wallets', 'webhooks'),
                        ('wallets', 'reporting'),
                        ('inventario', ''),
                        ('inventario', 'catalog'),
                        ('inventario', 'variations'),
                        ('inventario', 'transactions'),
                        ('inventario', 'low_stock_alerts'),
                        ('inventario', 'warehousing'),
                        ('inventario', 'stock_balances'),
                        ('inventario', 'movements'),
                        ('inventario', 'reservations'),
                        ('inventario', 'fulfillment'),
                        ('inventario', 'purchasing'),
                        ('inventario', 'traceability'),
                        ('inventario', 'planning'),
                        ('inventario', 'reporting'),
                        ('inventario', 'negative_stock'),
                        ('pos', ''),
                        ('pos', 'registers'),
                        ('pos', 'sales'),
                        ('pos', 'carts'),
                        ('pos', 'returns'),
                        ('pos', 'reporting'),
                        ('communications', ''),
                        ('communications', 'chat'),
                        ('communications', 'audio_video'),
                        ('community', ''),
                        ('payments', ''),
                        ('notifications', ''),
                        ('attendance', ''),
                        ('storage', ''),
                        ('identity', ''),
                        ('identity', 'users'),
                        ('identity', 'credentials'),
                        ('identity', 'roles'),
                        ('identity', 'tenants'),
                        ('identity', 'sessions'),
                        ('identity', 'verifications'),
                        ('identity', 'contacts'),
                        ('identity', 'addresses'),
                        ('identity', 'auth_logs')
                ),
                capabilities("CapabilityKey") AS (
                    VALUES ('view'), ('create'), ('update'), ('delete'), ('manage')
                )
                INSERT INTO "Identity"."IdentityRoleTypeFeaturePermission"
                    ("ID", "TenantId", "RoleTypeId", "ModuleKey", "SubFeatureKey", "CapabilityKey", "Effect", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt")
                SELECT
                    uuid_generate_v4(),
                    role_type."TenantId",
                    role_type."ID",
                    features."ModuleKey",
                    features."SubFeatureKey",
                    capabilities."CapabilityKey",
                    1,
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now()
                FROM "Identity"."IdentityRoleType" role_type
                CROSS JOIN features
                CROSS JOIN capabilities
                WHERE role_type."SystemReferenceId" = '6e7b6bf5-6ad6-49fb-80b0-38e967fc35f3'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."IdentityRoleTypeFeaturePermission" permission
                      WHERE permission."TenantId" = role_type."TenantId"
                        AND permission."RoleTypeId" = role_type."ID"
                        AND permission."ModuleKey" = features."ModuleKey"
                        AND permission."SubFeatureKey" = features."SubFeatureKey"
                        AND permission."CapabilityKey" = capabilities."CapabilityKey"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityRoleFeaturePermissionOverride",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "IdentityRoleTypeFeaturePermission",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "TenantAuthorizationPolicy",
                schema: "Identity");
        }
    }
}
