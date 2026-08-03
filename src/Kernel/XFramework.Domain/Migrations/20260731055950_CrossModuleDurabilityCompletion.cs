using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class CrossModuleDurabilityCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UnclaimedUntil",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyRequestId",
                schema: "Communications",
                table: "MessageDirect",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StorageClaimOutboxMessage",
                schema: "Identity",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    StorageFileId = table.Column<Guid>(type: "uuid", nullable: false),
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
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageClaimOutboxMessage", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "ix_storageuploadsession_global_expired_due",
                schema: "Storage",
                table: "StorageUploadSession",
                columns: new[] { "ExpiresAt", "CreatedAt" },
                filter: "\"AbortedAt\" IS NULL AND \"Status\" IN (0, 1, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "ix_storagefile_global_unclaimed_due",
                schema: "Storage",
                table: "StorageFile",
                columns: new[] { "UnclaimedUntil", "CreatedAt" },
                filter: "\"UnclaimedUntil\" IS NOT NULL AND \"ObjectDeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MessageDirect_Tenant_IdempotencyRequest",
                schema: "Communications",
                table: "MessageDirect",
                columns: new[] { "TenantId", "IdempotencyRequestId" },
                unique: true,
                filter: "\"IdempotencyRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StorageClaimOutbox_Global_Due",
                schema: "Identity",
                table: "StorageClaimOutboxMessage",
                columns: new[] { "NextAttemptAt", "LeaseExpiresAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_StorageClaimOutbox_Tenant_File_Request",
                schema: "Identity",
                table: "StorageClaimOutboxMessage",
                columns: new[] { "TenantId", "StorageFileId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageClaimOutboxMessage",
                schema: "Identity");

            migrationBuilder.DropIndex(
                name: "ix_storageuploadsession_global_expired_due",
                schema: "Storage",
                table: "StorageUploadSession");

            migrationBuilder.DropIndex(
                name: "ix_storagefile_global_unclaimed_due",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropIndex(
                name: "UX_MessageDirect_Tenant_IdempotencyRequest",
                schema: "Communications",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "UnclaimedUntil",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "IdempotencyRequestId",
                schema: "Communications",
                table: "MessageDirect");
        }
    }
}
