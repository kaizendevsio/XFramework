using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerOutboxRetryAndDueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchStartedAt",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchStartedAt",
                schema: "Identity",
                table: "PasswordResetOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationDeliveryOutbox_Global_Due",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage",
                columns: new[] { "NextAttemptAt", "LeaseExpiresAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_StorageCleanupOutbox_Global_Due",
                schema: "Identity",
                table: "StorageCleanupOutboxMessage",
                columns: new[] { "NextAttemptAt", "LeaseExpiresAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOutbox_Global_Due",
                schema: "Identity",
                table: "PasswordResetOutboxMessage",
                columns: new[] { "NextAttemptAt", "LeaseExpiresAt", "CreatedAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"IsDeleted\" = FALSE AND \"IsEnabled\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VerificationDeliveryOutbox_Global_Due",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_StorageCleanupOutbox_Global_Due",
                schema: "Identity",
                table: "StorageCleanupOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_PasswordResetOutbox_Global_Due",
                schema: "Identity",
                table: "PasswordResetOutboxMessage");

            migrationBuilder.DropColumn(
                name: "DispatchStartedAt",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "Identity",
                table: "VerificationDeliveryOutboxMessage");

            migrationBuilder.DropColumn(
                name: "DispatchStartedAt",
                schema: "Identity",
                table: "PasswordResetOutboxMessage");
        }
    }
}
