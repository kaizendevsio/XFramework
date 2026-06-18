using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using XFramework.Domain.Contexts;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617222322_InventarioFoundationCore")]
    public partial class InventarioFoundationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Inventario");

            migrationBuilder.CreateTable(
                name: "ProductCategory",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_ProductCategory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Warehouse",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_Inventario_Warehouse", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SKU = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Rating = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_Inventario_Product", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_Product_Category",
                        column: x => x.CategoryId,
                        principalSchema: "Inventario",
                        principalTable: "ProductCategory",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLocation",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LocationType = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    IsPickable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_Inventario_InventoryLocation", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryLocation_Parent",
                        column: x => x.ParentLocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryLocation_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductTransaction",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_Inventario_ProductTransaction", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ProductTransaction_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariation",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AdditionalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_Inventario_ProductVariation", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_ProductVariation_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockBalance",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnHandQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LastMovementAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Inventario_StockBalance", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_StockBalance_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_StockBalance_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_StockBalance_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovement",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockBalanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityBefore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UnitOfMeasure = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Inventario_InventoryMovement", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryMovement_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryMovement_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryMovement_StockBalance",
                        column: x => x.StockBalanceId,
                        principalSchema: "Inventario",
                        principalTable: "StockBalance",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_InventoryMovement_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservation",
                schema: "Inventario",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockBalanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Inventario_Reservation", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Inventario_Reservation_Location",
                        column: x => x.LocationId,
                        principalSchema: "Inventario",
                        principalTable: "InventoryLocation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_Reservation_Product",
                        column: x => x.ProductId,
                        principalSchema: "Inventario",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_Reservation_StockBalance",
                        column: x => x.StockBalanceId,
                        principalSchema: "Inventario",
                        principalTable: "StockBalance",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inventario_Reservation_Warehouse",
                        column: x => x.WarehouseId,
                        principalSchema: "Inventario",
                        principalTable: "Warehouse",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            CreateIndexes(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InventoryMovement", schema: "Inventario");
            migrationBuilder.DropTable(name: "ProductTransaction", schema: "Inventario");
            migrationBuilder.DropTable(name: "ProductVariation", schema: "Inventario");
            migrationBuilder.DropTable(name: "Reservation", schema: "Inventario");
            migrationBuilder.DropTable(name: "StockBalance", schema: "Inventario");
            migrationBuilder.DropTable(name: "InventoryLocation", schema: "Inventario");
            migrationBuilder.DropTable(name: "Product", schema: "Inventario");
            migrationBuilder.DropTable(name: "Warehouse", schema: "Inventario");
            migrationBuilder.DropTable(name: "ProductCategory", schema: "Inventario");
        }

        private static void CreateIndexes(MigrationBuilder migrationBuilder)
        {
            CreateTenantIndexes(migrationBuilder, "ProductCategory");
            CreateTenantIndexes(migrationBuilder, "Warehouse");
            CreateTenantIndexes(migrationBuilder, "Product");
            CreateTenantIndexes(migrationBuilder, "InventoryLocation");
            CreateTenantIndexes(migrationBuilder, "ProductTransaction");
            CreateTenantIndexes(migrationBuilder, "ProductVariation");
            CreateTenantIndexes(migrationBuilder, "StockBalance");
            CreateTenantIndexes(migrationBuilder, "InventoryMovement");
            CreateTenantIndexes(migrationBuilder, "Reservation");

            migrationBuilder.CreateIndex("IX_ProductCategory_TenantId_Name", "ProductCategory", new[] { "TenantId", "Name" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Warehouse_TenantId_Code", "Warehouse", new[] { "TenantId", "Code" }, schema: "Inventario", unique: true);
            migrationBuilder.CreateIndex("IX_Product_CategoryId", "Product", "CategoryId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Product_TenantId_CategoryId", "Product", new[] { "TenantId", "CategoryId" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Product_TenantId_SKU", "Product", new[] { "TenantId", "SKU" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryLocation_ParentLocationId", "InventoryLocation", "ParentLocationId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryLocation_WarehouseId", "InventoryLocation", "WarehouseId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryLocation_TenantId_WarehouseId_Code", "InventoryLocation", new[] { "TenantId", "WarehouseId", "Code" }, schema: "Inventario", unique: true);
            migrationBuilder.CreateIndex("IX_InventoryLocation_TenantId_ParentLocationId", "InventoryLocation", new[] { "TenantId", "ParentLocationId" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_ProductTransaction_ProductId", "ProductTransaction", "ProductId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_ProductTransaction_TenantId_ProductId_TransactionDate", "ProductTransaction", new[] { "TenantId", "ProductId", "TransactionDate" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_ProductVariation_ProductId", "ProductVariation", "ProductId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_ProductVariation_TenantId_ProductId", "ProductVariation", new[] { "TenantId", "ProductId" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_StockBalance_LocationId", "StockBalance", "LocationId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_StockBalance_ProductId", "StockBalance", "ProductId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_StockBalance_WarehouseId", "StockBalance", "WarehouseId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_StockBalance_TenantId_ProductId_WarehouseId_LocationId", "StockBalance", new[] { "TenantId", "ProductId", "WarehouseId", "LocationId" }, schema: "Inventario", unique: true);
            migrationBuilder.CreateIndex("IX_InventoryMovement_LocationId", "InventoryMovement", "LocationId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryMovement_ProductId", "InventoryMovement", "ProductId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryMovement_StockBalanceId", "InventoryMovement", "StockBalanceId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryMovement_WarehouseId", "InventoryMovement", "WarehouseId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryMovement_TenantId_ProductId_MovementDate", "InventoryMovement", new[] { "TenantId", "ProductId", "MovementDate" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_InventoryMovement_TenantId_ReferenceType_ReferenceId", "InventoryMovement", new[] { "TenantId", "ReferenceType", "ReferenceId" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_LocationId", "Reservation", "LocationId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_ProductId", "Reservation", "ProductId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_StockBalanceId", "Reservation", "StockBalanceId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_WarehouseId", "Reservation", "WarehouseId", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_ExpiresAt", "Reservation", "ExpiresAt", schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_TenantId_ProductId_Status", "Reservation", new[] { "TenantId", "ProductId", "Status" }, schema: "Inventario");
            migrationBuilder.CreateIndex("IX_Reservation_TenantId_ReferenceType_ReferenceId", "Reservation", new[] { "TenantId", "ReferenceType", "ReferenceId" }, schema: "Inventario");
        }

        private static void CreateTenantIndexes(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.CreateIndex($"IX_{table}_TenantId", table, "TenantId", schema: "Inventario");
            migrationBuilder.CreateIndex($"IX_{table}_TenantId_IsDeleted", table, new[] { "TenantId", "IsDeleted" }, schema: "Inventario");
        }
    }
}
