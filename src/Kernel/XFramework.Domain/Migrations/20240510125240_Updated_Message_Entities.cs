using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Message_Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "messagedirect_identitycredential_2_id_fk",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropForeignKey(
                name: "messagedirect_messagedirect_id_fk",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "Recipient",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "Sender",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.AlterColumn<Guid>(
                name: "TypeId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentMessageId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ExternalRecipient",
                schema: "Messaging",
                table: "MessageDirect",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageTransportType",
                schema: "Messaging",
                table: "MessageDirect",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "messagedirect_identitycredential_2_id_fk",
                schema: "Messaging",
                table: "MessageDirect",
                column: "RecipientId",
                principalSchema: "Identity",
                principalTable: "IdentityCredential",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "messagedirect_messagedirect_id_fk",
                schema: "Messaging",
                table: "MessageDirect",
                column: "ParentMessageId",
                principalSchema: "Messaging",
                principalTable: "MessageDirect",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "messagedirect_identitycredential_2_id_fk",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropForeignKey(
                name: "messagedirect_messagedirect_id_fk",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "ExternalRecipient",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "MessageTransportType",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.AlterColumn<Guid>(
                name: "TypeId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentMessageId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                schema: "Messaging",
                table: "MessageDirect",
                type: "character varying",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sender",
                schema: "Messaging",
                table: "MessageDirect",
                type: "character varying",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "messagedirect_identitycredential_2_id_fk",
                schema: "Messaging",
                table: "MessageDirect",
                column: "RecipientId",
                principalSchema: "Identity",
                principalTable: "IdentityCredential",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "messagedirect_messagedirect_id_fk",
                schema: "Messaging",
                table: "MessageDirect",
                column: "ParentMessageId",
                principalSchema: "Messaging",
                principalTable: "MessageDirect",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
