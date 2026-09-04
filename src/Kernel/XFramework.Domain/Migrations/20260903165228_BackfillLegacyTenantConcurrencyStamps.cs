using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLegacyTenantConcurrencyStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Application"."Application"
                SET "ConcurrencyStamp" = uuid_generate_v4()
                WHERE "ConcurrencyStamp" = '00000000-0000-0000-0000-000000000000'::uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Generated concurrency stamps are data repairs and cannot be safely reversed.
        }
    }
}
