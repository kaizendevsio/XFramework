using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerBackendHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_identitycredentials_avatar_storagefile_fk",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropIndex(
                name: "IX_ServiceSigningKey_IsActive",
                schema: "Identity",
                table: "ServiceSigningKey");

            migrationBuilder.DropIndex(
                name: "IX_IdentityCredential_TenantId",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropIndex(
                name: "tbl_identitycredentials_un",
                schema: "Identity",
                table: "IdentityCredential");

            // Existing bearer/session secrets and verification codes were stored in plaintext.
            // They cannot be migrated into hashes without the original presented value, so expire them.
            migrationBuilder.Sql("UPDATE \"Identity\".\"Session\" SET \"SessionData\" = NULL;");
            migrationBuilder.Sql("UPDATE \"Identity\".\"IdentityVerification\" SET \"Token\" = NULL;");

            // Database-held private keys are intentionally retired. IdentityServer bootstraps a
            // replacement into its protected key directory after this migration.
            migrationBuilder.Sql("DELETE FROM \"Identity\".\"ServiceSigningKey\";");

            migrationBuilder.DropColumn(
                name: "PrivateKeyPem",
                schema: "Identity",
                table: "ServiceSigningKey");

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                schema: "Identity",
                table: "Session",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateKeyFileName",
                schema: "Identity",
                table: "ServiceSigningKey",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying",
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"Identity\".\"IdentityVerification\" " +
                "ALTER COLUMN \"StatusUpdatedOn\" TYPE timestamp with time zone " +
                "USING NULL::timestamp with time zone;");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                schema: "Identity",
                table: "IdentityCredential",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                schema: "Identity",
                table: "IdentityCredential",
                type: "timestamp with time zone",
                nullable: true);

            // Preserve existing file references while collapsing legacy duplicate metadata rows.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileType"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                UPDATE "Storage"."StorageFile" AS file
                SET "TypeId" = ranked.canonical_id
                FROM ranked
                WHERE file."TypeId" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;

                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileType"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                DELETE FROM "Storage"."StorageFileType" AS item
                USING ranked
                WHERE item."ID" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;

                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileIdentifierGroup"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                UPDATE "Storage"."StorageFileIdentifier" AS identifier
                SET "GroupId" = ranked.canonical_id
                FROM ranked
                WHERE identifier."GroupId" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;

                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileIdentifierGroup"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                DELETE FROM "Storage"."StorageFileIdentifierGroup" AS item
                USING ranked
                WHERE item."ID" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;

                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileIdentifier"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                UPDATE "Storage"."StorageFile" AS file
                SET "StorageFileIdentifierId" = ranked.canonical_id
                FROM ranked
                WHERE file."StorageFileIdentifierId" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;

                WITH ranked AS (
                    SELECT "ID",
                           FIRST_VALUE("ID") OVER (
                               PARTITION BY "TenantId", "Name"
                               ORDER BY "CreatedAt", "ID") AS canonical_id
                    FROM "Storage"."StorageFileIdentifier"
                    WHERE "Name" IS NOT NULL AND "IsDeleted" = false
                )
                DELETE FROM "Storage"."StorageFileIdentifier" AS item
                USING ranked
                WHERE item."ID" = ranked."ID"
                  AND ranked."ID" <> ranked.canonical_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StorageFileType_TenantId_Name",
                schema: "Storage",
                table: "StorageFileType",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFileIdentifierGroup_TenantId_Name",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StorageFileIdentifier_TenantId_Name",
                schema: "Storage",
                table: "StorageFileIdentifier",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Session_TenantId_Status_ExpiresAt",
                schema: "Identity",
                table: "Session",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSigningKey_IsActive",
                schema: "Identity",
                table: "ServiceSigningKey",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification",
                columns: new[] { "TenantId", "Token", "Status", "Expiry" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityCredential_TenantId_UserName",
                schema: "Identity",
                table: "IdentityCredential",
                columns: new[] { "TenantId", "UserName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorageFileType_TenantId_Name",
                schema: "Storage",
                table: "StorageFileType");

            migrationBuilder.DropIndex(
                name: "IX_StorageFileIdentifierGroup_TenantId_Name",
                schema: "Storage",
                table: "StorageFileIdentifierGroup");

            migrationBuilder.DropIndex(
                name: "IX_StorageFileIdentifier_TenantId_Name",
                schema: "Storage",
                table: "StorageFileIdentifier");

            migrationBuilder.DropIndex(
                name: "IX_Session_TenantId_Status_ExpiresAt",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropIndex(
                name: "IX_ServiceSigningKey_IsActive",
                schema: "Identity",
                table: "ServiceSigningKey");

            migrationBuilder.DropIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropIndex(
                name: "IX_IdentityCredential_TenantId_UserName",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropColumn(
                name: "PrivateKeyFileName",
                schema: "Identity",
                table: "ServiceSigningKey");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.AddColumn<string>(
                name: "PrivateKeyPem",
                schema: "Identity",
                table: "ServiceSigningKey",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"Identity\".\"IdentityVerification\" " +
                "ALTER COLUMN \"StatusUpdatedOn\" TYPE time with time zone " +
                "USING NULL::time with time zone;");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSigningKey_IsActive",
                schema: "Identity",
                table: "ServiceSigningKey",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityCredential_TenantId",
                schema: "Identity",
                table: "IdentityCredential",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "tbl_identitycredentials_un",
                schema: "Identity",
                table: "IdentityCredential",
                column: "UserName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "tbl_identitycredentials_avatar_storagefile_fk",
                schema: "Identity",
                table: "IdentityCredential",
                column: "AvatarStorageFileId",
                principalSchema: "Storage",
                principalTable: "StorageFile",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
