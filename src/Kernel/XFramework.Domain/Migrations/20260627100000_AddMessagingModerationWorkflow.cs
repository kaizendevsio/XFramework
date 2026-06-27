using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627100000_AddMessagingModerationWorkflow")]
    public partial class AddMessagingModerationWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageReportAudit",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying", nullable: false),
                    ActorCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromStatus = table.Column<short>(type: "smallint", nullable: true),
                    ToStatus = table.Column<short>(type: "smallint", nullable: true),
                    Note = table.Column<string>(type: "character varying", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagereportaudit_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MessageModerationRule",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying", nullable: false),
                    MatchType = table.Column<string>(type: "character varying", nullable: false),
                    Pattern = table.Column<string>(type: "character varying", nullable: false),
                    Action = table.Column<string>(type: "character varying", nullable: false),
                    Description = table.Column<string>(type: "character varying", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messagemoderationrule_pk", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageReportAudit_Report_CreatedAt",
                schema: "Messaging",
                table: "MessageReportAudit",
                columns: new[] { "ReportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageModerationRule_Tenant_Name_Active",
                schema: "Messaging",
                table: "MessageModerationRule",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageReportAudit",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "MessageModerationRule",
                schema: "Messaging");
        }
    }
}
