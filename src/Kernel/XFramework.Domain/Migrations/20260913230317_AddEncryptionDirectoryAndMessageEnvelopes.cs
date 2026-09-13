using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionDirectoryAndMessageEnvelopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EncryptionRequired",
                schema: "Communications",
                table: "MessageThread",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedEnvelope",
                schema: "Communications",
                table: "Message",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EncryptionAccount",
                schema: "Identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    DirectoryRevision = table.Column<long>(type: "bigint", nullable: false),
                    RootPublicKey = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    Roster = table.Column<string>(type: "character varying(524288)", maxLength: 524288, nullable: false),
                    DevicesJson = table.Column<string>(type: "character varying(1048576)", maxLength: 1048576, nullable: false),
                    RecoveryRevision = table.Column<long>(type: "bigint", nullable: false),
                    RecoveryArchive = table.Column<string>(type: "character varying(2097152)", maxLength: 2097152, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptionAccount", x => new { x.TenantId, x.CredentialId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncryptionAccount",
                schema: "Identity");

            migrationBuilder.DropColumn(
                name: "EncryptionRequired",
                schema: "Communications",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "EncryptedEnvelope",
                schema: "Communications",
                table: "Message");
        }
    }
}
