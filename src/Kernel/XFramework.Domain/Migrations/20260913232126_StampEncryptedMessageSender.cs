using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class StampEncryptedMessageSender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AcceptedSenderDirectoryRevision",
                schema: "Communications",
                table: "Message",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EncryptionSenderDeviceId",
                schema: "Communications",
                table: "Message",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedSenderDirectoryRevision",
                schema: "Communications",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "EncryptionSenderDeviceId",
                schema: "Communications",
                table: "Message");
        }
    }
}
