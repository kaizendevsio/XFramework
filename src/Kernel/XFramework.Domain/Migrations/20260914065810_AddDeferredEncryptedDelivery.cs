using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddDeferredEncryptedDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionAudienceJson",
                schema: "Communications",
                table: "Message",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "EncryptionOriginalEnvelopeHash",
                schema: "Communications",
                table: "Message",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingEncryptionCount",
                schema: "Communications",
                table: "Message",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingEncryptionMembersJson",
                schema: "Communications",
                table: "Message",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_Message_PendingEncryption",
                schema: "Communications",
                table: "Message",
                columns: new[] { "TenantId", "MessageThreadMemberId", "CreatedAt", "ID" },
                filter: "\"PendingEncryptionCount\" > 0 AND NOT \"IsDeleted\" AND \"IsEnabled\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Message_PendingEncryption",
                schema: "Communications",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "EncryptionAudienceJson",
                schema: "Communications",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "EncryptionOriginalEnvelopeHash",
                schema: "Communications",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "PendingEncryptionCount",
                schema: "Communications",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "PendingEncryptionMembersJson",
                schema: "Communications",
                table: "Message");
        }
    }
}
