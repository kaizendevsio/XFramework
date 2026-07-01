namespace POS.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PosCatalogItemResponse
{
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? VariantName { get; init; }
    public string? SKU { get; init; }
    public string? Brand { get; init; }
    public string? Image { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public bool IsAvailable { get; init; }
    public decimal Price { get; init; }
    public decimal? AvailableQuantity { get; init; }
}

[MemoryPackable]
public partial record PosRegisterResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public Guid MerchantCredentialId { get; init; }
    public Guid CashDrawerWalletId { get; init; }
    public Guid WalletTypeId { get; init; }
    public Guid CurrencyId { get; init; }
    public Guid DefaultWarehouseId { get; init; }
    public Guid DefaultLocationId { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
}

[MemoryPackable]
public partial record PosCartResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string CartNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public Guid CashierCredentialId { get; init; }
    public Guid? CustomerCredentialId { get; init; }
    public string? CustomerLabel { get; init; }
    public string? Notes { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public PosCartStatus Status { get; init; }
    public decimal SubtotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public Guid WalletTypeId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ResumedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? ConvertedSaleId { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public List<string> Warnings { get; init; } = [];
    public List<PosCartLineResponse> Lines { get; init; } = [];
}

[MemoryPackable]
public partial record PosCartLineResponse
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? VariantName { get; init; }
    public string? SKU { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal ExpectedUnitPrice { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotal { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? LotId { get; init; }
}

[MemoryPackable]
public partial record PosCartSummaryResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string CartNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public Guid CashierCredentialId { get; init; }
    public Guid? CustomerCredentialId { get; init; }
    public string? CustomerLabel { get; init; }
    public PosCartStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? ConvertedSaleId { get; init; }
    public Guid ConcurrencyStamp { get; init; }
}

[MemoryPackable]
public partial record PosSaleReceiptResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public Guid CashierCredentialId { get; init; }
    public Guid? CustomerCredentialId { get; init; }
    public PosSaleStatus Status { get; init; }
    public decimal SubtotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public Guid WalletTypeId { get; init; }
    public PosPaymentMethod PaymentMethod { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public DateTime? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public string? RecoveryState { get; init; }
    public List<PosSaleLineResponse> Lines { get; init; } = [];
    public List<PosPaymentResponse> Payments { get; init; } = [];
}

[MemoryPackable]
public partial record PosSaleLineResponse
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? VariantName { get; init; }
    public string? SKU { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotal { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? LotId { get; init; }
    public Guid? ReservationId { get; init; }
    public DateTime? FulfilledAt { get; init; }
    public string? FailureReason { get; init; }
}

[MemoryPackable]
public partial record PosPaymentResponse
{
    public Guid Id { get; init; }
    public PosPaymentMethod Method { get; init; }
    public PosPaymentStatus Status { get; init; }
    public decimal Amount { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
    public DateTime? CapturedAt { get; init; }
}

[MemoryPackable]
public partial record PosSaleSummaryResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public PosSaleStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
}

[MemoryPackable]
public partial record PosReturnResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid RegisterId { get; init; }
    public Guid CashierCredentialId { get; init; }
    public Guid? CustomerCredentialId { get; init; }
    public PosReturnStatus Status { get; init; }
    public PosPaymentMethod RefundMethod { get; init; }
    public decimal TotalRefundAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public Guid WalletTypeId { get; init; }
    public string? Reason { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public string? RefundReferenceNumber { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public List<PosReturnLineResponse> Lines { get; init; } = [];
}

[MemoryPackable]
public partial record PosReturnLineResponse
{
    public Guid Id { get; init; }
    public Guid SaleLineId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? ProductVariationId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? VariantName { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid LocationId { get; init; }
    public Guid? LotId { get; init; }
    public string? InventoryMovementReferenceNumber { get; init; }
    public string? FailureReason { get; init; }
}

[MemoryPackable]
public partial record PosReturnSummaryResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public Guid SaleId { get; init; }
    public Guid RegisterId { get; init; }
    public PosReturnStatus Status { get; init; }
    public decimal TotalRefundAmount { get; init; }
    public Guid CurrencyId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
}
