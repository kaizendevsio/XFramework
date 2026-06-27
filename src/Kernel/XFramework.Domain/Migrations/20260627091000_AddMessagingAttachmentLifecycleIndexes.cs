using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627091000_AddMessagingAttachmentLifecycleIndexes")]
    public partial class AddMessagingAttachmentLifecycleIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MessageFiles_Tenant_Message_Created",
                schema: "Messaging",
                table: "MessageFiles",
                columns: new[] { "TenantId", "MessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageFiles_Message_Storage_Active",
                schema: "Messaging",
                table: "MessageFiles",
                columns: new[] { "MessageId", "StorageId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageFiles_Tenant_Message_Created",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropIndex(
                name: "UX_MessageFiles_Message_Storage_Active",
                schema: "Messaging",
                table: "MessageFiles");
        }
    }
}
