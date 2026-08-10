using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class StorageProductionHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_storagetenantbucket_tenant_provider",
                schema: "Storage",
                table: "StorageTenantBucket");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Storage",
                table: "StorageUploadPart",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                schema: "Storage",
                table: "StorageTenantBucket",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "Storage"."StorageFile"
                SET "PublicUrl" = NULL, "CdnBaseUrl" = NULL
                WHERE "Visibility" = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_storagetenantbucket_tenant_provider",
                schema: "Storage",
                table: "StorageTenantBucket",
                columns: new[] { "TenantId", "ProviderProfileId", "Purpose" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_storagetenantbucket_tenant_provider",
                schema: "Storage",
                table: "StorageTenantBucket");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Storage",
                table: "StorageUploadPart");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "Storage",
                table: "StorageTenantBucket");

            migrationBuilder.CreateIndex(
                name: "ix_storagetenantbucket_tenant_provider",
                schema: "Storage",
                table: "StorageTenantBucket",
                columns: new[] { "TenantId", "ProviderProfileId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
