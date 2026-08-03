using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityContactAuthenticationIndexOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.CreateIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact",
                columns: new[] { "TenantId", "Value", "TypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsEnabled\" = true AND \"TypeId\" IN ('03f26cc1-e4c2-424f-9d5b-b22d006ae45b'::uuid, 'cdc88887-c7e7-415e-9d43-cc0050d523d3'::uuid)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.CreateIndex(
                name: "UX_IdentityContact_ActiveAuthenticationContact",
                schema: "Identity",
                table: "IdentityContact",
                columns: new[] { "TenantId", "TypeId", "Value" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsEnabled\" = true AND \"TypeId\" IN ('03f26cc1-e4c2-424f-9d5b-b22d006ae45b'::uuid, 'cdc88887-c7e7-415e-9d43-cc0050d523d3'::uuid)");
        }
    }
}
