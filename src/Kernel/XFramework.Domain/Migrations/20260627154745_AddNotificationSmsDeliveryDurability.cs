using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSmsDeliveryDurability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Notifications");

            migrationBuilder.EnsureSchema(
                name: "SmsGateway");

            migrationBuilder.CreateTable(
                name: "NotificationProviderSetting",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: true),
                    LastHealthCheckAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHealthStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("notificationprovidersetting_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SmsOutboundJob",
                schema: "SmsGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    AgentClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sender = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Recipient = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Intent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeasedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NotificationDeliveryJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("smsoutboundjob_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryJob",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    NotificationInboxItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RecipientAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeasedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("notificationdeliveryjob_pk", x => x.ID);
                    table.ForeignKey(
                        name: "notificationdeliveryjob_inboxitem_id_fk",
                        column: x => x.NotificationInboxItemId,
                        principalSchema: "Notifications",
                        principalTable: "NotificationInboxItem",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmsDeliveryAttempt",
                schema: "SmsGateway",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SmsOutboundJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("smsdeliveryattempt_pk", x => x.ID);
                    table.ForeignKey(
                        name: "smsdeliveryattempt_outboundjob_id_fk",
                        column: x => x.SmsOutboundJobId,
                        principalSchema: "SmsGateway",
                        principalTable: "SmsOutboundJob",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempt",
                schema: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    NotificationDeliveryJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("notificationdeliveryattempt_pk", x => x.ID);
                    table.ForeignKey(
                        name: "notificationdeliveryattempt_job_id_fk",
                        column: x => x.NotificationDeliveryJobId,
                        principalSchema: "Notifications",
                        principalTable: "NotificationDeliveryJob",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempt_NotificationDeliveryJobId",
                schema: "Notifications",
                table: "NotificationDeliveryAttempt",
                column: "NotificationDeliveryJobId");

            migrationBuilder.CreateIndex(
                name: "ux_notificationdeliveryattempt_tenant_job_attempt",
                schema: "Notifications",
                table: "NotificationDeliveryAttempt",
                columns: new[] { "TenantId", "NotificationDeliveryJobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryJob_NotificationInboxItemId",
                schema: "Notifications",
                table: "NotificationDeliveryJob",
                column: "NotificationInboxItemId");

            migrationBuilder.CreateIndex(
                name: "ix_notificationdeliveryjob_tenant_status_nextattempt",
                schema: "Notifications",
                table: "NotificationDeliveryJob",
                columns: new[] { "TenantId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "ux_notificationdeliveryjob_tenant_correlation",
                schema: "Notifications",
                table: "NotificationDeliveryJob",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true,
                filter: "\"CorrelationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_notificationdeliveryjob_tenant_item_channel",
                schema: "Notifications",
                table: "NotificationDeliveryJob",
                columns: new[] { "TenantId", "NotificationInboxItemId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryStatus_NotificationInboxItemId",
                schema: "Notifications",
                table: "NotificationDeliveryStatus",
                column: "NotificationInboxItemId");

            migrationBuilder.CreateIndex(
                name: "ux_notificationprovidersetting_tenant_channel_key",
                schema: "Notifications",
                table: "NotificationProviderSetting",
                columns: new[] { "TenantId", "Channel", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDeliveryAttempt_SmsOutboundJobId",
                schema: "SmsGateway",
                table: "SmsDeliveryAttempt",
                column: "SmsOutboundJobId");

            migrationBuilder.CreateIndex(
                name: "ux_smsdeliveryattempt_tenant_job_attempt",
                schema: "SmsGateway",
                table: "SmsDeliveryAttempt",
                columns: new[] { "TenantId", "SmsOutboundJobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_smsoutboundjob_tenant_cluster_status_nextattempt",
                schema: "SmsGateway",
                table: "SmsOutboundJob",
                columns: new[] { "TenantId", "AgentClusterId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "ix_smsoutboundjob_tenant_notification_delivery_job",
                schema: "SmsGateway",
                table: "SmsOutboundJob",
                columns: new[] { "TenantId", "NotificationDeliveryJobId" });

            migrationBuilder.CreateIndex(
                name: "ux_smsoutboundjob_tenant_correlation",
                schema: "SmsGateway",
                table: "SmsOutboundJob",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true,
                filter: "\"CorrelationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveryStatus_NotificationInboxItemId",
                schema: "Notifications",
                table: "NotificationDeliveryStatus");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempt",
                schema: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationProviderSetting",
                schema: "Notifications");

            migrationBuilder.DropTable(
                name: "SmsDeliveryAttempt",
                schema: "SmsGateway");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryJob",
                schema: "Notifications");

            migrationBuilder.DropTable(
                name: "SmsOutboundJob",
                schema: "SmsGateway");

        }
    }
}
