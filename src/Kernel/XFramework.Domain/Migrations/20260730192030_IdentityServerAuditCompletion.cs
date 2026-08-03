using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IdentityServerAuditCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsumedAt",
                schema: "Identity",
                table: "IdentityVerification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                schema: "Identity",
                table: "IdentityVerification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Identity"."IdentityVerification"
                SET "Purpose" = 'contact-verification'
                WHERE "Purpose" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { true, new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489") });

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { true, new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4") });

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { true, new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be") });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification",
                columns: new[] { "TenantId", "Purpose", "Token", "Status", "Expiry" });

            migrationBuilder.Sql(
                """
                WITH ranked_roles AS (
                    SELECT "ID",
                           ROW_NUMBER() OVER (
                               PARTITION BY "TenantId", "UserCredID", "RoleTypeID"
                               ORDER BY "CreatedAt" DESC, "ID" DESC) AS row_number
                    FROM "Identity"."IdentityRole"
                    WHERE "IsDeleted" = false AND "RoleTypeID" IS NOT NULL
                )
                UPDATE "Identity"."IdentityRole" AS role
                SET "IsDeleted" = true,
                    "IsEnabled" = false,
                    "DeletedAt" = now(),
                    "ModifiedAt" = now(),
                    "ConcurrencyStamp" = uuid_generate_v4()
                FROM ranked_roles
                WHERE role."ID" = ranked_roles."ID" AND ranked_roles.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityRole_Tenant_Credential_Type",
                schema: "Identity",
                table: "IdentityRole",
                columns: new[] { "TenantId", "UserCredID", "RoleTypeID" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityContact_Tenant_Value_Type",
                schema: "Identity",
                table: "IdentityContact",
                columns: new[] { "TenantId", "Value", "TypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationLog_TenantId_CreatedAt",
                schema: "Audit",
                table: "AuthorizationLog",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropIndex(
                name: "IX_IdentityRole_Tenant_Credential_Type",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropIndex(
                name: "IX_IdentityContact_Tenant_Value_Type",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropIndex(
                name: "IX_AuthorizationLog_TenantId_CreatedAt",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                schema: "Identity",
                table: "IdentityVerification",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0000-000000000000") });

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0000-000000000000") });

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"),
                columns: new[] { "IsEnabled", "SystemReferenceId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0000-000000000000") });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerification_TenantId_TokenHash_Status_Expiry",
                schema: "Identity",
                table: "IdentityVerification",
                columns: new[] { "TenantId", "Token", "Status", "Expiry" });
        }
    }
}
