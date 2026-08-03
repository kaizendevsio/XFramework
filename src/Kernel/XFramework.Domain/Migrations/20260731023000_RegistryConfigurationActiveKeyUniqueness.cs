using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731023000_RegistryConfigurationActiveKeyUniqueness")]
public partial class RegistryConfigurationActiveKeyUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_RegistryConfiguration_Tenant_Key",
            schema: "Registry",
            table: "RegistryConfiguration");

        migrationBuilder.CreateIndex(
            name: "UX_RegistryConfiguration_Tenant_Key",
            schema: "Registry",
            table: "RegistryConfiguration",
            columns: new[] { "TenantId", "Key" },
            unique: true,
            filter: "\"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_RegistryConfiguration_Tenant_Key",
            schema: "Registry",
            table: "RegistryConfiguration");

        migrationBuilder.CreateIndex(
            name: "UX_RegistryConfiguration_Tenant_Key",
            schema: "Registry",
            table: "RegistryConfiguration",
            columns: new[] { "TenantId", "Key" },
            unique: true);
    }
}
