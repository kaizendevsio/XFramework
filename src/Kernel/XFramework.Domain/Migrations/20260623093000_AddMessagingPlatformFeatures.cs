using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260623093000_AddMessagingPlatformFeatures")]
    public partial class AddMessagingPlatformFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentMessageId",
                schema: "Messaging",
                table: "Message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentionedCredentialIdsJson",
                schema: "Messaging",
                table: "Message",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MutedAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "character varying",
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.CreateTable(
                name: "MessageThreadInvite",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagethreadinvite_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessagePin",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagepin_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageSaved",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageThreadMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagesaved_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageReport",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying", nullable: false),
                    Details = table.Column<string>(type: "character varying", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagereport_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageBlock",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    BlockerCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messageblock_pk", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Message_ParentMessageId",
                schema: "Messaging",
                table: "Message",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadInvite_Thread_Credential_Status",
                schema: "Messaging",
                table: "MessageThreadInvite",
                columns: new[] { "MessageThreadId", "InvitedCredentialId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_MessagePin_Thread_Message_Active",
                schema: "Messaging",
                table: "MessagePin",
                columns: new[] { "MessageThreadId", "MessageId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_MessageSaved_Message_Member_Active",
                schema: "Messaging",
                table: "MessageSaved",
                columns: new[] { "MessageId", "MessageThreadMemberId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReport_Tenant_Status_CreatedAt",
                schema: "Messaging",
                table: "MessageReport",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageBlock_Blocker_Blocked_Active",
                schema: "Messaging",
                table: "MessageBlock",
                columns: new[] { "BlockerCredentialId", "BlockedCredentialId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "message_parent_message_id_fk",
                schema: "Messaging",
                table: "Message",
                column: "ParentMessageId",
                principalSchema: "Messaging",
                principalTable: "Message",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "message_parent_message_id_fk",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropTable(name: "MessageThreadInvite", schema: "Messaging");
            migrationBuilder.DropTable(name: "MessagePin", schema: "Messaging");
            migrationBuilder.DropTable(name: "MessageSaved", schema: "Messaging");
            migrationBuilder.DropTable(name: "MessageReport", schema: "Messaging");
            migrationBuilder.DropTable(name: "MessageBlock", schema: "Messaging");

            migrationBuilder.DropIndex(
                name: "IX_Message_ParentMessageId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(name: "ParentMessageId", schema: "Messaging", table: "Message");
            migrationBuilder.DropColumn(name: "MentionedCredentialIdsJson", schema: "Messaging", table: "Message");
            migrationBuilder.DropColumn(name: "IsMuted", schema: "Messaging", table: "MessageThreadMember");
            migrationBuilder.DropColumn(name: "MutedAt", schema: "Messaging", table: "MessageThreadMember");
            migrationBuilder.DropColumn(name: "IsArchived", schema: "Messaging", table: "MessageThreadMember");
            migrationBuilder.DropColumn(name: "ArchivedAt", schema: "Messaging", table: "MessageThreadMember");
            migrationBuilder.DropColumn(name: "LastSeenAt", schema: "Messaging", table: "MessageThreadMember");
            migrationBuilder.DropColumn(name: "Role", schema: "Messaging", table: "MessageThreadMember");
        }
    }
}
