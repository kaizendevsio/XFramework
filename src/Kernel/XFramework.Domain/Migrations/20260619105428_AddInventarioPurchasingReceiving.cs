using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioPurchasingReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Supplier",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_Supplier", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrder",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_PurchaseOrder", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_PurchaseOrder_Supplier",
                        column: x => x.SupplierId,
                        principalSchema: "Inventario",
                        principalTable: "Supplier",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLine",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_PurchaseOrderLine", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_PurchaseOrderLine_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "Inventario",
                        principalTable: "PurchaseOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingDocument",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_ReceivingDocument", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingDocument_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingDocument_PurchaseOrder",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "Inventario",
                        principalTable: "PurchaseOrder",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingDocument_Supplier",
                        column: x => x.SupplierId,
                        principalSchema: "Inventario",
                        principalTable: "Supplier",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingDocument_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingLine",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReceivingDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockBalanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_ReceivingLine", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingLine_InventoryMovement",
                        column: x => x.InventoryMovementId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryMovement",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingLine_Lot",
                        column: x => x.LotId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLot",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingLine_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingLine_PurchaseOrderLine",
                        column: x => x.PurchaseOrderLineId,
                        principalSchema: "Inventario",
                        principalTable: "PurchaseOrderLine",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReceivingLine_StockBalance",
                        column: x => x.StockBalanceId,
                        principalSchema: "Inventario",
                        principalTable: "StockBalance",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivingLine_ReceivingDocument_ReceivingDocumentId",
                        column: x => x.ReceivingDocumentId,
                        principalSchema: "Inventario",
                        principalTable: "ReceivingDocument",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupplierId",
                schema: "Inventario",
                table: "PurchaseOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_TenantId",
                schema: "Inventario",
                table: "PurchaseOrder",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_TenantId_IsDeleted",
                schema: "Inventario",
                table: "PurchaseOrder",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_TenantId_OrderNumber",
                schema: "Inventario",
                table: "PurchaseOrder",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_TenantId_Status",
                schema: "Inventario",
                table: "PurchaseOrder",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_ProductId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_PurchaseOrderId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId_IsDeleted",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId_ProductId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_TenantId_PurchaseOrderId",
                schema: "Inventario",
                table: "PurchaseOrderLine",
                columns: new[] { "TenantId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_LocationId",
                schema: "Inventario",
                table: "ReceivingDocument",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_PurchaseOrderId",
                schema: "Inventario",
                table: "ReceivingDocument",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_SupplierId",
                schema: "Inventario",
                table: "ReceivingDocument",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_TenantId",
                schema: "Inventario",
                table: "ReceivingDocument",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_TenantId_IdempotencyKey",
                schema: "Inventario",
                table: "ReceivingDocument",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_TenantId_IsDeleted",
                schema: "Inventario",
                table: "ReceivingDocument",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_TenantId_PurchaseOrderId",
                schema: "Inventario",
                table: "ReceivingDocument",
                columns: new[] { "TenantId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_TenantId_ReceiptNumber",
                schema: "Inventario",
                table: "ReceivingDocument",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingDocument_WarehouseId",
                schema: "Inventario",
                table: "ReceivingDocument",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_InventoryMovementId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_LotId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_ProductId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_PurchaseOrderLineId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_ReceivingDocumentId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "ReceivingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_StockBalanceId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "StockBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId",
                schema: "Inventario",
                table: "ReceivingLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_IsDeleted",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_ProductId_LotId",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "ProductId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_PurchaseOrderLineId",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingLine_TenantId_ReceivingDocumentId",
                schema: "Inventario",
                table: "ReceivingLine",
                columns: new[] { "TenantId", "ReceivingDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_TenantId",
                schema: "Inventario",
                table: "Supplier",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_TenantId_Code",
                schema: "Inventario",
                table: "Supplier",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_TenantId_IsDeleted",
                schema: "Inventario",
                table: "Supplier",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_TenantId_Name",
                schema: "Inventario",
                table: "Supplier",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivingLine",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLine",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "ReceivingDocument",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "PurchaseOrder",
                schema: "Inventario");

            migrationBuilder.DropTable(
                name: "Supplier",
                schema: "Inventario");
        }
    }
}
