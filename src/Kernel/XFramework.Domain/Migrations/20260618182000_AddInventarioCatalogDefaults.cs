using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260618182000_AddInventarioCatalogDefaults")]
    public partial class AddInventarioCatalogDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddConcurrencyStampDefault(migrationBuilder, "Product");
            AddConcurrencyStampDefault(migrationBuilder, "ProductCategory");
            AddConcurrencyStampDefault(migrationBuilder, "ProductTransaction");
            AddConcurrencyStampDefault(migrationBuilder, "ProductVariation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveConcurrencyStampDefault(migrationBuilder, "Product");
            RemoveConcurrencyStampDefault(migrationBuilder, "ProductCategory");
            RemoveConcurrencyStampDefault(migrationBuilder, "ProductTransaction");
            RemoveConcurrencyStampDefault(migrationBuilder, "ProductVariation");
        }

        private static void AddConcurrencyStampDefault(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Inventario",
                table: table,
                type: "uuid",
                nullable: false,
                defaultValueSql: "(uuid_generate_v4())",
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        private static void RemoveConcurrencyStampDefault(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Inventario",
                table: table,
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "(uuid_generate_v4())");
        }
    }
}
