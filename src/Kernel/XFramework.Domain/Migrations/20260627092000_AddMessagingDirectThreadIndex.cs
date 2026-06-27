using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627092000_AddMessagingDirectThreadIndex")]
    public partial class AddMessagingDirectThreadIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageDirectThread",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagedirectthread_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagedirectthread_messagethread_id_fk",
                        column: x => x.MessageThreadId,
                        principalSchema: "Messaging",
                        principalTable: "MessageThread",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "UX_MessageDirectThread_Tenant_Pair_Active",
                schema: "Messaging",
                table: "MessageDirectThread",
                columns: new[] { "TenantId", "FirstCredentialId", "SecondCredentialId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_MessageDirectThread_Thread_Active",
                schema: "Messaging",
                table: "MessageDirectThread",
                column: "MessageThreadId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageDirectThread",
                schema: "Messaging");
        }
    }
}
