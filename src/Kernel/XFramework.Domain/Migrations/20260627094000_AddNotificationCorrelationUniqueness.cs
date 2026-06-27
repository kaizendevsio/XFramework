using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260627094000_AddNotificationCorrelationUniqueness")]
public partial class AddNotificationCorrelationUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Notifications"."NotificationInboxItem" item
            USING (
                SELECT ctid,
                       row_number() OVER (
                           PARTITION BY "TenantId", "CorrelationId"
                           ORDER BY "CreatedAt", "ID"
                       ) AS row_number
                FROM "Notifications"."NotificationInboxItem"
                WHERE "CorrelationId" IS NOT NULL
            ) duplicate
            WHERE item.ctid = duplicate.ctid
              AND duplicate.row_number > 1;
            """);

        migrationBuilder.DropIndex(
            name: "ix_notificationinboxitem_tenant_correlation",
            schema: "Notifications",
            table: "NotificationInboxItem");

        migrationBuilder.CreateIndex(
            name: "ix_notificationinboxitem_tenant_correlation",
            schema: "Notifications",
            table: "NotificationInboxItem",
            columns: new[] { "TenantId", "CorrelationId" },
            unique: true,
            filter: "\"CorrelationId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_notificationinboxitem_tenant_correlation",
            schema: "Notifications",
            table: "NotificationInboxItem");

        migrationBuilder.CreateIndex(
            name: "ix_notificationinboxitem_tenant_correlation",
            schema: "Notifications",
            table: "NotificationInboxItem",
            columns: new[] { "TenantId", "CorrelationId" });
    }
}
