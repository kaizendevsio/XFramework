using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioReservationAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationAllocation",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredLotOverrideReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_ReservationAllocation", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_Lot",
                        column: x => x.LotId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLot",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_Reservation",
                        column: x => x.ReservationId,
                        principalSchema: "Inventario",
                        principalTable: "Reservation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_StockBalance",
                        column: x => x.StockBalanceId,
                        principalSchema: "Inventario",
                        principalTable: "StockBalance",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_ReservationAllocation_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_LocationId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_LotId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_ProductId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_ReservationId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_StockBalanceId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "StockBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_TenantId_IsDeleted",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "IsDeleted" });

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
                name: "IX_ReservationAllocation_TenantId_ReservationId_Status",
                schema: "Inventario",
                table: "ReservationAllocation",
                columns: new[] { "TenantId", "ReservationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationAllocation_WarehouseId",
                schema: "Inventario",
                table: "ReservationAllocation",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationAllocation",
                schema: "Inventario");
        }
    }
}
