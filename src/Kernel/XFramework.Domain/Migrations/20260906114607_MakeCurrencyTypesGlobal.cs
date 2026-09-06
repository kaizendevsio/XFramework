using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class MakeCurrencyTypesGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Finance"."CurrencyType"
                SET "TenantId" = '00000000-0000-0000-0000-000000000000'
                WHERE "TenantId" <> '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Currency types are shared reference data. Their former tenant ownership
            // cannot be reconstructed safely once the rows have been made global.
        }
    }
}
