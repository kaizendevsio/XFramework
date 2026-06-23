using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioVariantInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId_LotId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_ReservationAllocation_TenantId_LotId_Status",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropIndex(
                name: "IX_ReservationAllocation_TenantId_ProductId_WarehouseId_Locati~",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_TenantId_ProductId_Status",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_ReceivingLine_TenantId_ProductId_LotId",
                schema: "Inventario",
                table: "ReceivingLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_TenantId_ProductId",
                schema: "Inventario",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_WarehouseId_Locatio~",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_LotId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_ProductId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ExpiresAt",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_TenantId_ProductId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "StockBalance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ReservationAllocation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "Reservation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ReceivingLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                schema: "Inventario",
                table: "ProductVariation",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ProductTransaction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryMovement",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryLot",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductVariationType",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inventario_ProductVariationType", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ProductVariationType_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Inventario"."ProductVariationType" (
                    "ID",
                    "Name",
                    "NormalizedName",
                    "Code",
                    "ProductId",
                    "IsEnabled",
                    "IsDeleted",
                    "ConcurrencyStamp",
                    "CreatedAt",
                    "ModifiedAt",
                    "TenantId")
                SELECT
                    uuid_generate_v4(),
                    source."Name",
                    upper(source."Name"),
                    left(upper(source."Name"), 50),
                    source."ProductId",
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now(),
                    source."TenantId"
                FROM (
                    SELECT DISTINCT
                        pv."TenantId",
                        pv."ProductId",
                        COALESCE(NULLIF(btrim(pv."VariationType"), ''), 'Variant') AS "Name"
                    FROM "Inventario"."ProductVariation" pv
                    WHERE pv."IsDeleted" = false
                ) source;
                """);

            migrationBuilder.Sql("""
                UPDATE "Inventario"."ProductVariation" pv
                SET "Price" = COALESCE(p."Price", 0) + COALESCE(pv."AdditionalPrice", 0)
                FROM "Inventario"."Product" p
                WHERE p."ID" = pv."ProductId";
                """);

            migrationBuilder.Sql("""
                UPDATE "Inventario"."ProductVariation" pv
                SET "ProductVariationTypeId" = pvt."ID",
                    "VariationType" = pvt."Name"
                FROM "Inventario"."ProductVariationType" pvt
                WHERE pvt."TenantId" = pv."TenantId"
                  AND pvt."ProductId" = pv."ProductId"
                  AND pvt."NormalizedName" = upper(COALESCE(NULLIF(btrim(pv."VariationType"), ''), 'Variant'))
                  AND pv."ProductVariationTypeId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_ProductVariationId",
                schema: "Inventario",
                table: "StockBalance",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_ProductVariationId_Warehou~1",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId", "LotId" },
                unique: true,
                filter: "\"ProductVariationId\" IS NOT NULL AND \"LotId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_ProductVariationId_Warehous~",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "\"ProductVariationId\" IS NOT NULL AND \"LotId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "\"ProductVariationId\" IS NULL AND \"LotId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId_LotId",
                schema: "Inventario",
                table: "StockBalance",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId", "LotId" },
                unique: true,
                filter: "\"ProductVariationId\" IS NULL AND \"LotId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_ProductVariationId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId_LotId_ProductVariationId_Sta~",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "LotId", "ProductVariationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId_ProductId_ProductVariationId~",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_ProductVariationId",
                schema: "Inventario",
                table: "Reservation",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_TenantId_ProductId_ProductVariationId_Status",
                schema: "Inventario",
                table: "Reservation",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_ProductVariationId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_ProductId_ProductVariationId_LotId",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId_ProductId_ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariation_ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation",
                column: "ProductVariationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariation_TenantId_ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation",
                columns: new[] { "TenantId", "ProductVariationTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductTransaction_ProductVariationId",
                schema: "Inventario",
                table: "ProductTransaction",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTransaction_TenantId_ProductId_ProductVariationId_Tr~",
                schema: "Inventario",
                table: "ProductTransaction",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_ProductVariationId",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "WarehouseId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_ProductVariationId",
                schema: "Inventario",
                table: "InventoryMovement",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_LotId_ProductVariationId_Movemen~",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "LotId", "ProductVariationId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_ProductId_ProductVariationId_Mov~",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_ProductVariationId",
                schema: "Inventario",
                table: "InventoryLot",
                column: "ProductVariationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_ProductId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "ProductId", "LotNumber" },
                unique: true,
                filter: "\"ProductVariationId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ProductVariationId_ExpiresAt",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ProductVariationId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId", "LotNumber" },
                unique: true,
                filter: "\"ProductVariationId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_ProductId",
                schema: "Inventario",
                table: "ProductVariationType",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_TenantId",
                schema: "Inventario",
                table: "ProductVariationType",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_TenantId_IsDeleted",
                schema: "Inventario",
                table: "ProductVariationType",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_TenantId_NormalizedName",
                schema: "Inventario",
                table: "ProductVariationType",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "\"ProductId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_TenantId_ProductId",
                schema: "Inventario",
                table: "ProductVariationType",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationType_TenantId_ProductId_NormalizedName",
                schema: "Inventario",
                table: "ProductVariationType",
                columns: new[] { "TenantId", "ProductId", "NormalizedName" },
                unique: true,
                filter: "\"ProductId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_InventoryLot_ProductVariation",
                schema: "Inventario",
                table: "InventoryLot",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_InventoryMovement_ProductVariation",
                schema: "Inventario",
                table: "InventoryMovement",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_InventoryReorderRule_ProductVariation",
                schema: "Inventario",
                table: "InventoryReorderRule",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_ProductTransaction_ProductVariation",
                schema: "Inventario",
                table: "ProductTransaction",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_ProductVariation_ProductVariationType",
                schema: "Inventario",
                table: "ProductVariation",
                column: "ProductVariationTypeId",
                principalSchema: "Inventario",
                principalTable: "ProductVariationType",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_PurchaseOrderLine_ProductVariation",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_ReceivingLine_ProductVariation",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_Reservation_ProductVariation",
                schema: "Inventario",
                table: "Reservation",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_ReservationAllocation_ProductVariation",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventario_StockBalance_ProductVariation",
                schema: "Inventario",
                table: "StockBalance",
                column: "ProductVariationId",
                principalSchema: "Inventario",
                principalTable: "ProductVariation",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_InventoryLot_ProductVariation",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_InventoryMovement_ProductVariation",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_InventoryReorderRule_ProductVariation",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_ProductTransaction_ProductVariation",
                schema: "Inventario",
                table: "ProductTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_ProductVariation_ProductVariationType",
                schema: "Inventario",
                table: "ProductVariation");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_PurchaseOrderLine_ProductVariation",
                schema: "Inventario",
                table: "PurchaseOrderLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_ReceivingLine_ProductVariation",
                schema: "Inventario",
                table: "ReceivingLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_Reservation_ProductVariation",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_ReservationAllocation_ProductVariation",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventario_StockBalance_ProductVariation",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropTable(
                name: "ProductVariationType",
                schema: "Inventario");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_ProductVariationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_ProductVariationId_Warehou~1",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_ProductVariationId_Warehous~",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId_LotId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropIndex(
                name: "IX_ReservationAllocation_ProductVariationId",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropIndex(
                name: "IX_ReservationAllocation_TenantId_LotId_ProductVariationId_Sta~",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropIndex(
                name: "IX_ReservationAllocation_TenantId_ProductId_ProductVariationId~",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_ProductVariationId",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_TenantId_ProductId_ProductVariationId_Status",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_ReceivingLine_ProductVariationId",
                schema: "Inventario",
                table: "ReceivingLine");

            migrationBuilder.DropIndex(
                name: "IX_ReceivingLine_TenantId_ProductId_ProductVariationId_LotId",
                schema: "Inventario",
                table: "ReceivingLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLine_TenantId_ProductId_ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariation_ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariation_TenantId_ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation");

            migrationBuilder.DropIndex(
                name: "IX_ProductTransaction_ProductVariationId",
                schema: "Inventario",
                table: "ProductTransaction");

            migrationBuilder.DropIndex(
                name: "IX_ProductTransaction_TenantId_ProductId_ProductVariationId_Tr~",
                schema: "Inventario",
                table: "ProductTransaction");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReorderRule_ProductVariationId",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_ProductVariationId_~",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_ProductVariationId",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_LotId_ProductVariationId_Movemen~",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_TenantId_ProductId_ProductVariationId_Mov~",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_ProductVariationId",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_TenantId_ProductId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ProductVariationId_ExpiresAt",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLot_TenantId_ProductId_ProductVariationId_LotNumber",
                schema: "Inventario",
                table: "InventoryLot");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "StockBalance");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ReservationAllocation");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ReceivingLine");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "Inventario",
                table: "ProductVariation");

            migrationBuilder.DropColumn(
                name: "ProductVariationTypeId",
                schema: "Inventario",
                table: "ProductVariation");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "ProductTransaction");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryReorderRule");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryMovement");

            migrationBuilder.DropColumn(
                name: "ProductVariationId",
                schema: "Inventario",
                table: "InventoryLot");

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

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId_LotId_Status",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "LotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId_ProductId_WarehouseId_Locati~",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_TenantId_ProductId_Status",
                schema: "Inventario",
                table: "Reservation",
                columns: new[] { "TenantId", "ProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_ProductId_LotId",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "ProductId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId_ProductId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReorderRule_TenantId_ProductId_WarehouseId_Locatio~",
                schema: "Inventario",
                table: "InventoryReorderRule",
                columns: new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_LotId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "LotId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_TenantId_ProductId_MovementDate",
                schema: "Inventario",
                table: "InventoryMovement",
                columns: new[] { "TenantId", "ProductId", "MovementDate" });

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
        }
    }
}
