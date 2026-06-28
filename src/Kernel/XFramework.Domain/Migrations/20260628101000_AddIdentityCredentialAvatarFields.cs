using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityCredentialAvatarFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvatarStorageFileId",
                schema: "Identity",
                table: "IdentityCredential",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvatarUpdatedAt",
                schema: "Identity",
                table: "IdentityCredential",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                schema: "Identity",
                table: "IdentityCredential",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_IdentityCredentials_AvatarStorageFileId",
                schema: "Identity",
                table: "IdentityCredential",
                column: "AvatarStorageFileId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_identitycredentials_avatar_storagefile_fk",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropIndex(
                name: "IX_tbl_IdentityCredentials_AvatarStorageFileId",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "AvatarStorageFileId",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "AvatarUpdatedAt",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                schema: "Identity",
                table: "IdentityCredential");
        }
    }
}
