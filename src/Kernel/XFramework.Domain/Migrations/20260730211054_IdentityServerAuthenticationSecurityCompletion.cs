using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerAuthenticationSecurityCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                schema: "Identity",
                table: "Session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Session_TenantId_Status_RefreshTokenExpiresAt",
                schema: "Identity",
                table: "Session",
                columns: new[] { "TenantId", "Status", "RefreshTokenExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Session_TenantId_Status_RefreshTokenExpiresAt",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                schema: "Identity",
                table: "Session");
        }
    }
}
