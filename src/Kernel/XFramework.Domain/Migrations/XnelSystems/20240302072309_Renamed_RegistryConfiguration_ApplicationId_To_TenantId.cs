using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class Renamed_RegistryConfiguration_ApplicationId_To_TenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_RegistryConfiguration_ApplicationId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "IX_RegistryConfiguration_TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "ApplicationId");

            migrationBuilder.RenameIndex(
                name: "IX_RegistryConfiguration_TenantId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "IX_RegistryConfiguration_ApplicationId");
        }
    }
}
