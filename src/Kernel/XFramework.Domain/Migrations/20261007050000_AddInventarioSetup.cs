using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

namespace XFramework.Domain.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261007050000_AddInventarioSetup")]
public sealed class AddInventarioSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InventarioSetup", schema: "Inventario",
            columns: table => new
            {
                ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Mode = table.Column<int>(type: "integer", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                LowStockThreshold = table.Column<int>(type: "integer", nullable: false),
                DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                CompletionRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                CompletionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Inventario_Setup", x => x.ID));
        migrationBuilder.CreateIndex("IX_InventarioSetup_TenantId", "InventarioSetup", "TenantId", "Inventario", unique: true);
        migrationBuilder.CreateIndex("IX_InventarioSetup_TenantId_IsDeleted", "InventarioSetup", new[] { "TenantId", "IsDeleted" }, "Inventario");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("InventarioSetup", "Inventario");
}
