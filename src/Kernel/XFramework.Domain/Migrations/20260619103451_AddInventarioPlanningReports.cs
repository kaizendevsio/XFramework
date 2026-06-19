using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioPlanningReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryReorderRule",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinimumQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MaximumQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreferredSupplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventario_InventoryReorderRule", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryReorderRule_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryReorderRule_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryReorderRule_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_LocationId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_ProductId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_IsActive",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_IsDeleted",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_WarehouseId_Locatio~",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_WarehouseId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryReorderRule",
                schema: "Inventario");
        }
    }
}
