using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Added_SentAt_RecievedAt_MessageDirect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecievedAt",
                schema: "Messaging",
                table: "MessageDirect",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                schema: "Messaging",
                table: "MessageDirect",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecievedAt",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "SentAt",
                schema: "Messaging",
                table: "MessageDirect");
        }
    }
}
