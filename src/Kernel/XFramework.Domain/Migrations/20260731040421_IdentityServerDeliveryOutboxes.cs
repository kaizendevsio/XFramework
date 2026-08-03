using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations;

public partial class IdentityServerDeliveryOutboxes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PasswordResetOutboxMessage",
            schema: "Identity",
            columns: table => new
            {
                ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PasswordResetOutboxMessage", x => x.ID));

        migrationBuilder.CreateTable(
            name: "VerificationDeliveryOutboxMessage",
            schema: "Identity",
            columns: table => new
            {
                ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                VerificationId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                TransportType = table.Column<int>(type: "integer", nullable: false),
                Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Intent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Message = table.Column<string>(type: "text", nullable: true),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VerificationDeliveryOutboxMessage", x => x.ID));

        migrationBuilder.CreateIndex(
            name: "IX_PasswordResetOutbox_Tenant_Due_Lease",
            schema: "Identity",
            table: "PasswordResetOutboxMessage",
            columns: new[] { "TenantId", "DeadLetteredAt", "ProcessedAt", "NextAttemptAt", "LeaseExpiresAt" });
        migrationBuilder.CreateIndex(
            name: "UX_PasswordResetOutbox_Tenant_Request",
            schema: "Identity",
            table: "PasswordResetOutboxMessage",
            columns: new[] { "TenantId", "RequestId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VerificationDeliveryOutbox_Tenant_Due_Lease",
            schema: "Identity",
            table: "VerificationDeliveryOutboxMessage",
            columns: new[] { "TenantId", "DeadLetteredAt", "ProcessedAt", "LeaseExpiresAt" });
        migrationBuilder.CreateIndex(
            name: "UX_VerificationDeliveryOutbox_Tenant_Verification",
            schema: "Identity",
            table: "VerificationDeliveryOutboxMessage",
            columns: new[] { "TenantId", "VerificationId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PasswordResetOutboxMessage", schema: "Identity");
        migrationBuilder.DropTable(name: "VerificationDeliveryOutboxMessage", schema: "Identity");
    }
}
