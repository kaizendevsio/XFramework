using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Features",
                schema: "Communications",
                table: "MessageThread",
                type: "integer",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<bool>(
                name: "IsThreadReply",
                schema: "Communications",
                table: "Message",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Both participants manage existing direct conversations, matching newly created ones.
            migrationBuilder.Sql("""
                UPDATE "Communications"."MessageThreadMember" AS member
                SET "Role" = 'Admin'
                FROM "Communications"."MessageDirectThread" AS direct
                WHERE member."MessageThreadId" = direct."MessageThreadId"
                  AND member."TenantId" = direct."TenantId"
                  AND member."Role" = 'Member'
                  AND NOT member."IsDeleted" AND member."IsEnabled"
                  AND NOT direct."IsDeleted" AND direct."IsEnabled";

                WITH candidates AS (
                    SELECT member."ID", ROW_NUMBER() OVER (
                        PARTITION BY member."TenantId", member."MessageThreadId" ORDER BY member."CreatedAt", member."ID") AS position
                    FROM "Communications"."MessageThreadMember" AS member
                    WHERE NOT member."IsDeleted" AND member."IsEnabled"
                      AND NOT EXISTS (
                        SELECT 1 FROM "Communications"."MessageThreadMember" AS admin
                        WHERE admin."TenantId" = member."TenantId" AND admin."MessageThreadId" = member."MessageThreadId"
                          AND NOT admin."IsDeleted" AND admin."IsEnabled" AND admin."Role" IN ('Owner', 'Admin'))
                )
                UPDATE "Communications"."MessageThreadMember" AS member SET "Role" = 'Admin'
                FROM candidates WHERE candidates."ID" = member."ID" AND candidates.position = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Features",
                schema: "Communications",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "IsThreadReply",
                schema: "Communications",
                table: "Message");
        }
    }
}
