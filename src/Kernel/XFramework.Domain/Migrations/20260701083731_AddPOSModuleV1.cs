using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPOSModuleV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "POS");

            migrationBuilder.CreateTable(
                name: "PosRegister",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MerchantCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashDrawerWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_POS_Register", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PosSale",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SaleNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecoveryState = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_POS_Sale", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_Sale_Register",
                        column: x => x.RegisterId,
                        principalSchema: "POS",
                        principalTable: "PosRegister",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosPayment",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_POS_Payment", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_Payment_Sale",
                        column: x => x.SaleId,
                        principalSchema: "POS",
                        principalTable: "PosSale",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosReturn",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReturnNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RefundMethod = table.Column<int>(type: "integer", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RefundReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_POS_Return", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_Return_Register",
                        column: x => x.RegisterId,
                        principalSchema: "POS",
                        principalTable: "PosRegister",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POS_Return_Sale",
                        column: x => x.SaleId,
                        principalSchema: "POS",
                        principalTable: "PosSale",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosSaleLine",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_POS_SaleLine", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_SaleLine_Sale",
                        column: x => x.SaleId,
                        principalSchema: "POS",
                        principalTable: "PosSale",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosReturnLine",
                schema: "POS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    VariantName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryMovementReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_POS_ReturnLine", x => x.ID);
                    table.ForeignKey(
                        name: "FK_POS_ReturnLine_Return",
                        column: x => x.ReturnId,
                        principalSchema: "POS",
                        principalTable: "PosReturn",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POS_ReturnLine_SaleLine",
                        column: x => x.SaleLineId,
                        principalSchema: "POS",
                        principalTable: "PosSaleLine",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POS_Payment_Tenant_Sale",
                schema: "POS",
                table: "PosPayment",
                columns: new[] { "TenantId", "SaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosPayment_SaleId",
                schema: "POS",
                table: "PosPayment",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PosPayment_TenantId",
                schema: "POS",
                table: "PosPayment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosPayment_TenantId_IsDeleted",
                schema: "POS",
                table: "PosPayment",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_Payment_Tenant_Idempotency_Active",
                schema: "POS",
                table: "PosPayment",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PosRegister_TenantId",
                schema: "POS",
                table: "PosRegister",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosRegister_TenantId_IsDeleted",
                schema: "POS",
                table: "PosRegister",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_Register_Tenant_Code_Active",
                schema: "POS",
                table: "PosRegister",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_POS_Return_Tenant_Status_Created",
                schema: "POS",
                table: "PosReturn",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosReturn_RegisterId",
                schema: "POS",
                table: "PosReturn",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturn_SaleId",
                schema: "POS",
                table: "PosReturn",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturn_TenantId",
                schema: "POS",
                table: "PosReturn",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturn_TenantId_IsDeleted",
                schema: "POS",
                table: "PosReturn",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_Return_Tenant_Idempotency_Active",
                schema: "POS",
                table: "PosReturn",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");

            migrationBuilder.CreateIndex(
                name: "UX_POS_Return_Tenant_Number_Active",
                schema: "POS",
                table: "PosReturn",
                columns: new[] { "TenantId", "ReturnNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_POS_ReturnLine_Tenant_Return",
                schema: "POS",
                table: "PosReturnLine",
                columns: new[] { "TenantId", "ReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_POS_ReturnLine_Tenant_SaleLine",
                schema: "POS",
                table: "PosReturnLine",
                columns: new[] { "TenantId", "SaleLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLine_ReturnId",
                schema: "POS",
                table: "PosReturnLine",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLine_SaleLineId",
                schema: "POS",
                table: "PosReturnLine",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLine_TenantId",
                schema: "POS",
                table: "PosReturnLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLine_TenantId_IsDeleted",
                schema: "POS",
                table: "PosReturnLine",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_POS_Sale_Tenant_Status_Created",
                schema: "POS",
                table: "PosSale",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PosSale_RegisterId",
                schema: "POS",
                table: "PosSale",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_PosSale_TenantId",
                schema: "POS",
                table: "PosSale",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosSale_TenantId_IsDeleted",
                schema: "POS",
                table: "PosSale",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_Sale_Tenant_Idempotency_Active",
                schema: "POS",
                table: "PosSale",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");

            migrationBuilder.CreateIndex(
                name: "UX_POS_Sale_Tenant_Number_Active",
                schema: "POS",
                table: "PosSale",
                columns: new[] { "TenantId", "SaleNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_POS_SaleLine_Tenant_Reservation",
                schema: "POS",
                table: "PosSaleLine",
                columns: new[] { "TenantId", "ReservationId" },
                filter: "\"ReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosSaleLine_SaleId",
                schema: "POS",
                table: "PosSaleLine",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PosSaleLine_TenantId",
                schema: "POS",
                table: "PosSaleLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PosSaleLine_TenantId_IsDeleted",
                schema: "POS",
                table: "PosSaleLine",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_POS_SaleLine_Tenant_Sale_Line_Active",
                schema: "POS",
                table: "PosSaleLine",
                columns: new[] { "TenantId", "SaleId", "LineNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosPayment",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosReturnLine",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosReturn",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosSaleLine",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosSale",
                schema: "POS");

            migrationBuilder.DropTable(
                name: "PosRegister",
                schema: "POS");
        }
    }
}
