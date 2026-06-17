using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617000000_AddNotificationsBaseline")]
    public partial class AddNotificationsBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Notifications");

            migrationBuilder.CreateTable(
                name: "NotificationInboxItem",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    RecipientCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DeliveryChannels = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DataJson = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notificationinboxitem_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreference",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnabledChannels = table.Column<int>(type: "integer", nullable: false),
                    DisabledTemplateKeys = table.Column<string>(type: "text", nullable: true),
                    DigestEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notificationpreference_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryStatus",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    NotificationInboxItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notificationdeliverystatus_pk", x => x.ID);
                    table.ForeignKey(
                        name: "notificationdeliverystatus_inboxitem_id_fk",
                        column: x => x.NotificationInboxItemId,
                        principalSchema: "Notifications",
                        principalTable: "NotificationInboxItem",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificationinboxitem_tenant_correlation",
                schema: "Notifications",
                table: "NotificationInboxItem",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "ix_notificationinboxitem_tenant_recipient_read_created",
                schema: "Notifications",
                table: "NotificationInboxItem",
                columns: new[] { "TenantId", "RecipientCredentialId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_notificationinboxitem_tenant_template",
                schema: "Notifications",
                table: "NotificationInboxItem",
                columns: new[] { "TenantId", "TemplateKey" });

            migrationBuilder.CreateIndex(
                name: "ux_notificationpreference_tenant_credential",
                schema: "Notifications",
                table: "NotificationPreference",
                columns: new[] { "TenantId", "CredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_notificationdeliverystatus_tenant_item_channel",
                schema: "Notifications",
                table: "NotificationDeliveryStatus",
                columns: new[] { "TenantId", "NotificationInboxItemId", "Channel" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryStatus",
                schema: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationPreference",
                schema: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationInboxItem",
                schema: "Notifications");
        }
    }
}
