using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617142635_AddTenantModuleFeatures")]
    public partial class AddTenantModuleFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantModuleFeature",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ModuleKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false),
                    SubFeatureKey = table.Column<string>(type: "character varying", maxLength: 128, nullable: false, defaultValue: ""),
                    DisplayName = table.Column<string>(type: "character varying", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying", nullable: true),
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
                    table.PrimaryKey("tenantmodulefeature_pk", x => x.ID);
                    table.ForeignKey(
                        name: "tenantmodulefeature_application_tenantid_fk",
                        column: x => x.TenantId,
                        principalSchema: "Application",
                        principalTable: "Application",
                        principalColumn: "ID");
                });

            migrationBuilder.Sql(
                """
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
                    tenants."ID"
                FROM "Application"."Application" tenants
                CROSS JOIN (VALUES
                    ('wallets', '', 'Wallets', 'Wallet accounts, balances, transfers, deposits, and withdrawals.'),
                    ('inventario', '', 'Inventario', 'Product catalog and inventory operations.'),
                    ('messaging', 'chat', 'Messaging Chat', 'Threads, direct messages, reactions, and attachments.'),
                    ('messaging', 'audio_video', 'Messaging Audio/Video', 'Audio and video communication features.'),
                    ('community', '', 'Community', 'Community identities, content, feed, and connections.'),
                    ('payments', '', 'Payments', 'Payment gateway and cash-in/cash-out capabilities.'),
                    ('notifications', '', 'Notifications', 'Tenant notifications and read-state workflows.')
                ) AS definitions("ModuleKey", "SubFeatureKey", "DisplayName", "Description")
                WHERE tenants."IsDeleted" = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = tenants."ID"
                        AND existing."ModuleKey" = definitions."ModuleKey"
                        AND existing."SubFeatureKey" = definitions."SubFeatureKey"
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TenantModuleFeature_Tenant_Module_SubFeature",
                schema: "Identity",
                table: "TenantModuleFeature",
                columns: new[] { "TenantId", "ModuleKey", "SubFeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantModuleFeature",
                schema: "Identity");
        }
    }
}
