using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627090000_AddMessagingOutboxDurability")]
    public partial class AddMessagingOutboxDurability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotificationAttempts",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotificationProcessedAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RealtimeAttempts",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RealtimeProcessedAt",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Messaging"."MessageOutboxEvent"
                SET "RealtimeProcessedAt" = "ProcessedAt",
                    "NotificationProcessedAt" = "ProcessedAt"
                WHERE "ProcessedAt" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MessageOutboxEvent_Tenant_Retry_Lease",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                columns: new[] { "TenantId", "DeadLetteredAt", "NextAttemptAt", "LeaseExpiresAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageOutboxEvent_Tenant_Retry_Lease",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "NotificationAttempts",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "NotificationProcessedAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "RealtimeAttempts",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.DropColumn(
                name: "RealtimeProcessedAt",
                schema: "Messaging",
                table: "MessageOutboxEvent");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                type: "character varying",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
