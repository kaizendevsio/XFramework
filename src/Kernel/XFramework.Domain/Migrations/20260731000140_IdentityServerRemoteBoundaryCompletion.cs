using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerRemoteBoundaryCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Session_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "Session",
                columns: new[] { "TenantId", "CreatedAt", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerification_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "IdentityVerification",
                columns: new[] { "TenantId", "CreatedAt", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityCredential_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "IdentityCredential",
                columns: new[] { "TenantId", "CreatedAt", "ID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Session_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropIndex(
                name: "IX_IdentityVerification_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropIndex(
                name: "IX_IdentityCredential_TenantId_CreatedAt_Id",
                schema: "Identity",
                table: "IdentityCredential");
        }
    }
}
