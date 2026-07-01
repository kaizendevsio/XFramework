using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPOSCarts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosCart",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CartNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedSaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_POS_Cart", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_Cart_ConvertedSale",
                        column: x => x.ConvertedSaleId,
                        principalSchema: "POS",
                        principalTable: "PosSale",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_Cart_Register",
                        column: x => x.RegisterId,
                        principalSchema: "POS",
                        principalTable: "PosRegister",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosCartLine",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    VariantName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SKU = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_POS_CartLine", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_CartLine_Cart",
                        column: x => x.CartId,
                        principalSchema: "POS",
                        principalTable: "PosCart",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POS_Cart_Tenant_Cashier_Status_Created",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "CashierCredentialId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_POS_Cart_Tenant_Expires",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_POS_Cart_Tenant_Register_Status_Suspended",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "RegisterId", "Status", "SuspendedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosCart_ConvertedSaleId",
                schema: "POS",
                table: "PosCart",
                column: "ConvertedSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCart_RegisterId",
                schema: "POS",
                table: "PosCart",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCart_TenantId",
                schema: "POS",
                table: "PosCart",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCart_TenantId_IsDeleted",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_Cart_Tenant_Idempotency_Active",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");

            migrationBuilder.CreateIndex(
                name: "UX_POS_Cart_Tenant_Number_Active",
                schema: "POS",
                table: "PosCart",
                columns: new[] { "TenantId", "CartNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_POS_CartLine_Tenant_Product",
                schema: "POS",
                table: "PosCartLine",
                columns: new[] { "TenantId", "ProductId", "ProductVariationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosCartLine_CartId",
                schema: "POS",
                table: "PosCartLine",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCartLine_TenantId",
                schema: "POS",
                table: "PosCartLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCartLine_TenantId_IsDeleted",
                schema: "POS",
                table: "PosCartLine",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_CartLine_Tenant_Cart_Line_Active",
                schema: "POS",
                table: "PosCartLine",
                columns: new[] { "TenantId", "CartId", "LineNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosCartLine",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosCart",
                schema: "POS");
        }
    }
}
