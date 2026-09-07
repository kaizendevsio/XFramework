using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;
namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260907090000_AddAuditViewerAdminPermission")]
public sealed class AddAuditViewerAdminPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO "Identity"."IdentityRoleTypeFeaturePermission"
            ("ID", "TenantId", "RoleTypeId", "ModuleKey", "SubFeatureKey", "CapabilityKey", "Effect",
             "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt")
        SELECT gen_random_uuid(), role_type."TenantId", role_type."ID", 'audit', '', 'view', 1,
               true, false, gen_random_uuid(), now(), now()
        FROM "Identity"."IdentityRoleType" role_type
        WHERE role_type."SystemReferenceId" = '6e7b6bf5-6ad6-49fb-80b0-38e967fc35f3'
          AND NOT role_type."IsDeleted" AND role_type."IsEnabled"
        ON CONFLICT ("TenantId", "RoleTypeId", "ModuleKey", "SubFeatureKey", "CapabilityKey") DO NOTHING;
        """);
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve assignments: a later administrator may have intentionally modified these grants.
    }
}
