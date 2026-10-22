using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261022010000_EnableExistingReservationFulfillment")]
public sealed class EnableExistingReservationFulfillment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        WITH enabled_fulfillment AS (
            INSERT INTO "Identity"."TenantModuleFeature" AS target
                ("ID", "TenantId", "ModuleKey", "SubFeatureKey", "DisplayName", "Description",
                 "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt")
            SELECT gen_random_uuid(), reservations."TenantId", 'inventario', 'fulfillment', 'Fulfillment',
                   'Ship reserved inventory and complete its allocations; no separate pick/pack workflow.',
                   true, false, gen_random_uuid(), now(), now()
            FROM "Identity"."TenantModuleFeature" reservations
            JOIN "Application"."Application" tenant
              ON tenant."ID" = reservations."TenantId" AND tenant."IsEnabled" AND NOT tenant."IsDeleted"
            JOIN "Identity"."TenantModuleFeature" root
              ON root."TenantId" = reservations."TenantId"
             AND root."ModuleKey" = 'inventario' AND root."SubFeatureKey" = ''
             AND root."IsEnabled" AND NOT root."IsDeleted"
            WHERE reservations."ModuleKey" = 'inventario' AND reservations."SubFeatureKey" = 'reservations'
              AND reservations."IsEnabled" AND NOT reservations."IsDeleted"
            ON CONFLICT ("TenantId", "ModuleKey", "SubFeatureKey") DO UPDATE
            SET "IsEnabled" = true, "ConcurrencyStamp" = gen_random_uuid(), "ModifiedAt" = now()
            WHERE NOT target."IsEnabled" AND NOT target."IsDeleted"
            RETURNING "TenantId"
        )
        UPDATE "Application"."Application" tenant
        SET "ConcurrencyStamp" = gen_random_uuid(), "ModifiedAt" = now()
        WHERE tenant."ID" IN (SELECT "TenantId" FROM enabled_fulfillment);
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve access and later administrator decisions; the previous service did not gate fulfillment.
    }
}
