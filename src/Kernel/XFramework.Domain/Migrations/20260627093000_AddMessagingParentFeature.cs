using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627093000_AddMessagingParentFeature")]
    public partial class AddMessagingParentFeature : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Identity"."TenantModuleFeature" (
                    "ID",
                    "TenantId",
                    "ModuleKey",
                    "SubFeatureKey",
                    "DisplayName",
                    "Description",
                    "IsEnabled",
                    "IsDeleted",
                    "CreatedAt",
                    "ModifiedAt",
                    "ConcurrencyStamp")
                SELECT
                    uuid_generate_v4(),
                    chat."TenantId",
                    'messaging',
                    '',
                    'Messaging',
                    'Tenant messaging settings, administration, moderation, and chat platform controls.',
                    chat."IsEnabled",
                    false,
                    now(),
                    now(),
                    uuid_generate_v4()
                FROM "Identity"."TenantModuleFeature" chat
                WHERE chat."ModuleKey" = 'messaging'
                  AND chat."SubFeatureKey" = 'chat'
                  AND chat."IsDeleted" = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Identity"."TenantModuleFeature" existing
                      WHERE existing."TenantId" = chat."TenantId"
                        AND existing."ModuleKey" = 'messaging'
                        AND existing."SubFeatureKey" = ''
                        AND existing."IsDeleted" = false
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Identity"."TenantModuleFeature"
                SET "IsDeleted" = true,
                    "IsEnabled" = false,
                    "DeletedAt" = now(),
                    "ModifiedAt" = now()
                WHERE "ModuleKey" = 'messaging'
                  AND "SubFeatureKey" = ''
                  AND "DisplayName" = 'Messaging';
                """);
        }
    }
}
