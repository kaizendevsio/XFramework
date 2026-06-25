using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260625090000_AddMessagingTemplates")]
    public partial class AddMessagingTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                schema: "Messaging",
                table: "Message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                schema: "Messaging",
                table: "Message",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                schema: "Messaging",
                table: "Message",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVariablesJson",
                schema: "Messaging",
                table: "Message",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                schema: "Messaging",
                table: "MessageDirect",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                schema: "Messaging",
                table: "MessageDirect",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVariablesJson",
                schema: "Messaging",
                table: "MessageDirect",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.CreateTable(
                name: "MessageTemplate",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Key = table.Column<string>(type: "character varying", nullable: false),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    TemplateType = table.Column<string>(type: "character varying", nullable: false),
                    Subject = table.Column<string>(type: "character varying", nullable: true),
                    Body = table.Column<string>(type: "character varying", nullable: false),
                    RequiredVariablesJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    OwnerCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SystemReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("messagetemplate_pk", x => x.ID);
                    table.ForeignKey(
                        name: "messagetemplate_ownercredential_id_fk",
                        column: x => x.OwnerCredentialId,
                        principalSchema: "Identity",
                        principalTable: "IdentityCredential",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Message_TemplateId",
                schema: "Messaging",
                table: "Message",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDirect_TemplateId",
                schema: "Messaging",
                table: "MessageDirect",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_Tenant_Type_Key",
                schema: "Messaging",
                table: "MessageTemplate",
                columns: new[] { "TenantId", "TemplateType", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_Tenant_Type_ModifiedAt",
                schema: "Messaging",
                table: "MessageTemplate",
                columns: new[] { "TenantId", "TemplateType", "ModifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplate_OwnerCredentialId",
                schema: "Messaging",
                table: "MessageTemplate",
                column: "OwnerCredentialId");

            migrationBuilder.CreateIndex(
                name: "UX_MessageTemplate_Tenant_Type_Key_Active",
                schema: "Messaging",
                table: "MessageTemplate",
                columns: new[] { "TenantId", "TemplateType", "Key" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"OwnerCredentialId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MessageTemplate_User_Key_Active",
                schema: "Messaging",
                table: "MessageTemplate",
                columns: new[] { "TenantId", "OwnerCredentialId", "Key" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"OwnerCredentialId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageTemplate",
                schema: "Messaging");

            migrationBuilder.DropIndex(
                name: "IX_Message_TemplateId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "IX_MessageDirect_TemplateId",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "TemplateVariablesJson",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "TemplateVariablesJson",
                schema: "Messaging",
                table: "MessageDirect");
        }
    }
}
