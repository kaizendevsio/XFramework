using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerPersistenceRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegistryConfiguration_TenantId",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropIndex(
                name: "IX_IdentityContact_Tenant_Value_Type",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.Sql(
                """
                WITH ranked_configurations AS (
                    SELECT "ID",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "Key"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS row_number
                    FROM "Registry"."RegistryConfiguration"
                )
                DELETE FROM "Registry"."RegistryConfiguration" AS configuration
                USING ranked_configurations
                WHERE configuration."ID" = ranked_configurations."ID"
                  AND ranked_configurations.row_number > 1;
                """);

            migrationBuilder.Sql(
                """
                WITH ranked_contacts AS (
                    SELECT "ID",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "TypeId", "Value"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS row_number
                    FROM "Identity"."IdentityContact"
                    WHERE "IsDeleted" = false
                      AND "IsEnabled" = true
                      AND "TypeId" IN (
                          '03f26cc1-e4c2-424f-9d5b-b22d006ae45b'::uuid,
                          'cdc88887-c7e7-415e-9d43-cc0050d523d3'::uuid)
                )
                UPDATE "Identity"."IdentityContact" AS contact
                SET "IsDeleted" = true,
                    "IsEnabled" = false,
                    "DeletedAt" = now(),
                    "ModifiedAt" = now()
                FROM ranked_contacts
                WHERE contact."ID" = ranked_contacts."ID"
                  AND ranked_contacts.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_RegistryConfiguration_Tenant_Key",
                schema: "Registry",
                table: "RegistryConfiguration",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact",
                columns: new[] { "TenantId", "TypeId", "Value" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsEnabled\" = true AND \"TypeId\" IN ('03f26cc1-e4c2-424f-9d5b-b22d006ae45b'::uuid, 'cdc88887-c7e7-415e-9d43-cc0050d523d3'::uuid)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_RegistryConfiguration_Tenant_Key",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.CreateIndex(
                name: "IX_RegistryConfiguration_TenantId",
                schema: "Registry",
                table: "RegistryConfiguration",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityContact_Tenant_Value_Type",
                schema: "Identity",
                table: "IdentityContact",
                columns: new[] { "TenantId", "Value", "TypeId" });
        }
    }
}
