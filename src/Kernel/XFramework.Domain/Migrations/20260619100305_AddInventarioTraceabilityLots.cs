using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioTraceabilityLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryLot",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ManufacturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_Inventario_InventoryLot", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryLot_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                schema: "Inventario",
                table: "StockBalance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                schema: "Inventario",
                table: "InventoryMovement",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "Inventario",
                table: "InventoryMovement",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "Inventario",
                table: "InventoryMovement",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_ProductId",
                schema: "Inventario",
                table: "InventoryLot",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId",
                schema: "Inventario",
                table: "InventoryLot",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_IsDeleted",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ExpiresAt",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "ProductId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_ProductId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "ProductId", "LotNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_Status",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_LotId",
                schema: "Inventario",
                table: "InventoryMovement",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_IdempotencyKey",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_LotId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "LotId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_LotId",
                schema: "Inventario",
                table: "StockBalance",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "\"LotId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId_LotId",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId", "LotId" },
                unique: true,
                filter: "\"LotId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_InventoryMovement_Lot",
                schema: "Inventario",
                table: "InventoryMovement",
                column: "LotId",
                principalSchema: "Inventario",
                principalTable: "InventoryLot",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_StockBalance_Lot",
                schema: "Inventario",
                table: "StockBalance",
                column: "LotId",
                principalSchema: "Inventario",
                principalTable: "InventoryLot",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_InventoryMovement_Lot",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_StockBalance_Lot",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropTable(
                name: "InventoryLot",
                schema: "Inventario");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_IdempotencyKey",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_LotId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_LotId",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId_LotId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_LotId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropColumn(
                name: "LotId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropColumn(
                name: "LotId",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" },
                unique: true);
        }
    }
}
