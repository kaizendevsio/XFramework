using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerFinalAuditCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked_groups AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "SystemReferenceId"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS canonical_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "SystemReferenceId"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS row_number
                    FROM "Registry"."RegistryConfigurationGroup"
                    WHERE "IsDeleted" = false
                )
                UPDATE "Registry"."RegistryConfiguration" AS configuration
                SET "GroupId" = ranked_groups.canonical_id,
                    "ModifiedAt" = now()
                FROM ranked_groups
                WHERE configuration."GroupId" = ranked_groups."ID"
                  AND ranked_groups.row_number > 1;

                WITH ranked_groups AS (
                    SELECT "ID",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "SystemReferenceId"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS row_number
                    FROM "Registry"."RegistryConfigurationGroup"
                    WHERE "IsDeleted" = false
                )
                UPDATE "Registry"."RegistryConfigurationGroup" AS configuration_group
                SET "IsDeleted" = true,
                    "IsEnabled" = false,
                    "DeletedAt" = now(),
                    "ModifiedAt" = now()
                FROM ranked_groups
                WHERE configuration_group."ID" = ranked_groups."ID"
                  AND ranked_groups.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RegistryConfigurationGroup_TenantId_SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                columns: new[] { "TenantId", "SystemReferenceId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegistryConfigurationGroup_TenantId_SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfigurationGroup");
        }
    }
}
