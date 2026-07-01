namespace POS.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/pos/registers",
    RequireAuthorization = true,
    CacheDurationSeconds = 120,
    CacheKeyPrefix = "pos-registers"
)]
public partial class PosRegister : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string? Code { get; set; }

    [MemoryPackOrder(2)]
    public Guid MerchantCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public Guid CashDrawerWalletId { get; set; }

    [MemoryPackOrder(4)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(5)]
    public Guid CurrencyId { get; set; }

    [MemoryPackOrder(6)]
    public Guid DefaultWarehouseId { get; set; }

    [MemoryPackOrder(7)]
    public Guid DefaultLocationId { get; set; }

    [MemoryPackOrder(8)]
    public string? Description { get; set; }

    [MemoryPackIgnore]
    public virtual ICollection<PosSale> Sales { get; set; } = new List<PosSale>();

    [MemoryPackIgnore]
    public virtual ICollection<PosReturn> Returns { get; set; } = new List<PosReturn>();

    [MemoryPackIgnore]
    public virtual ICollection<PosCart> Carts { get; set; } = new List<PosCart>();
}

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/pos/carts",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "pos-carts"
)]
public partial class PosCart : BaseModel
{
    [MemoryPackOrder(0)]
    public string CartNumber { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public Guid RegisterId { get; set; }

    [MemoryPackOrder(2)]
    public Guid CashierCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? CustomerCredentialId { get; set; }

    [MemoryPackOrder(4)]
    public string? CustomerLabel { get; set; }

    [MemoryPackOrder(5)]
    public string? Notes { get; set; }

    [MemoryPackOrder(6)]
    public Guid WarehouseId { get; set; }

    [MemoryPackOrder(7)]
    public Guid LocationId { get; set; }

    [MemoryPackOrder(8)]
    public PosCartStatus Status { get; set; } = PosCartStatus.Open;

    [MemoryPackOrder(9)]
    public decimal SubtotalAmount { get; set; }

    [MemoryPackOrder(10)]
    public decimal DiscountAmount { get; set; }

    [MemoryPackOrder(11)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(12)]
    public decimal TotalAmount { get; set; }

    [MemoryPackOrder(13)]
    public Guid CurrencyId { get; set; }

    [MemoryPackOrder(14)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(15)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MemoryPackOrder(16)]
    public DateTime? SuspendedAt { get; set; }

    [MemoryPackOrder(17)]
    public DateTime? ResumedAt { get; set; }

    [MemoryPackOrder(18)]
    public DateTime? ExpiresAt { get; set; }

    [MemoryPackOrder(19)]
    public Guid? ConvertedSaleId { get; set; }

    [MemoryPackOrder(20)]
    public DateTime? CancelledAt { get; set; }

    [MemoryPackOrder(21)]
    public string? CancelReason { get; set; }

    [MemoryPackIgnore]
    public virtual PosRegister Register { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<PosCartLine> Lines { get; set; } = new List<PosCartLine>();
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class PosCartLine : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid CartId { get; set; }

    [MemoryPackOrder(1)]
    public int LineNumber { get; set; }

    [MemoryPackOrder(2)]
    public Guid ProductId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ProductVariationId { get; set; }

    [MemoryPackOrder(4)]
    public string ProductName { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public string? VariantName { get; set; }

    [MemoryPackOrder(6)]
    public string? SKU { get; set; }

    [MemoryPackOrder(7)]
    public decimal Quantity { get; set; }

    [MemoryPackOrder(8)]
    public decimal UnitPrice { get; set; }

    [MemoryPackOrder(9)]
    public decimal ExpectedUnitPrice { get; set; }

    [MemoryPackOrder(10)]
    public decimal DiscountAmount { get; set; }

    [MemoryPackOrder(11)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(12)]
    public decimal LineTotal { get; set; }

    [MemoryPackOrder(13)]
    public Guid WarehouseId { get; set; }

    [MemoryPackOrder(14)]
    public Guid LocationId { get; set; }

    [MemoryPackOrder(15)]
    public Guid? LotId { get; set; }

    [MemoryPackIgnore]
    public virtual PosCart Cart { get; set; } = null!;
}

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/pos/sales",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "pos-sales"
)]
public partial class PosSale : BaseModel
{
    [MemoryPackOrder(0)]
    public string SaleNumber { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public Guid RegisterId { get; set; }

    [MemoryPackOrder(2)]
    public Guid CashierCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? CustomerCredentialId { get; set; }

    [MemoryPackOrder(4)]
    public Guid WarehouseId { get; set; }

    [MemoryPackOrder(5)]
    public Guid LocationId { get; set; }

    [MemoryPackOrder(6)]
    public PosSaleStatus Status { get; set; } = PosSaleStatus.Draft;

    [MemoryPackOrder(7)]
    public decimal SubtotalAmount { get; set; }

    [MemoryPackOrder(8)]
    public decimal DiscountAmount { get; set; }

    [MemoryPackOrder(9)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(10)]
    public decimal TotalAmount { get; set; }

    [MemoryPackOrder(11)]
    public Guid CurrencyId { get; set; }

    [MemoryPackOrder(12)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(13)]
    public PosPaymentMethod PaymentMethod { get; set; }

    [MemoryPackOrder(14)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MemoryPackOrder(15)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(16)]
    public DateTime? CancelledAt { get; set; }

    [MemoryPackOrder(17)]
    public string? FailureReason { get; set; }

    [MemoryPackOrder(18)]
    public string? RecoveryState { get; set; }

    [MemoryPackIgnore]
    public virtual PosRegister Register { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<PosSaleLine> Lines { get; set; } = new List<PosSaleLine>();

    [MemoryPackIgnore]
    public virtual ICollection<PosPayment> Payments { get; set; } = new List<PosPayment>();

    [MemoryPackIgnore]
    public virtual ICollection<PosReturn> Returns { get; set; } = new List<PosReturn>();
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class PosSaleLine : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid SaleId { get; set; }

    [MemoryPackOrder(1)]
    public int LineNumber { get; set; }

    [MemoryPackOrder(2)]
    public Guid ProductId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ProductVariationId { get; set; }

    [MemoryPackOrder(4)]
    public string ProductName { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public string? VariantName { get; set; }

    [MemoryPackOrder(6)]
    public string? SKU { get; set; }

    [MemoryPackOrder(7)]
    public decimal Quantity { get; set; }

    [MemoryPackOrder(8)]
    public decimal UnitPrice { get; set; }

    [MemoryPackOrder(9)]
    public decimal ExpectedUnitPrice { get; set; }

    [MemoryPackOrder(10)]
    public decimal DiscountAmount { get; set; }

    [MemoryPackOrder(11)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(12)]
    public decimal LineTotal { get; set; }

    [MemoryPackOrder(13)]
    public Guid WarehouseId { get; set; }

    [MemoryPackOrder(14)]
    public Guid LocationId { get; set; }

    [MemoryPackOrder(15)]
    public Guid? LotId { get; set; }

    [MemoryPackOrder(16)]
    public Guid? ReservationId { get; set; }

    [MemoryPackOrder(17)]
    public DateTime? FulfilledAt { get; set; }

    [MemoryPackOrder(18)]
    public string? FailureReason { get; set; }

    [MemoryPackIgnore]
    public virtual PosSale Sale { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<PosReturnLine> ReturnLines { get; set; } = new List<PosReturnLine>();
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class PosPayment : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid SaleId { get; set; }

    [MemoryPackOrder(1)]
    public PosPaymentMethod Method { get; set; }

    [MemoryPackOrder(2)]
    public PosPaymentStatus Status { get; set; } = PosPaymentStatus.Pending;

    [MemoryPackOrder(3)]
    public decimal Amount { get; set; }

    [MemoryPackOrder(4)]
    public Guid CurrencyId { get; set; }

    [MemoryPackOrder(5)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(6)]
    public Guid? WalletId { get; set; }

    [MemoryPackOrder(7)]
    public Guid? CustomerCredentialId { get; set; }

    [MemoryPackOrder(8)]
    public Guid MerchantCredentialId { get; set; }

    [MemoryPackOrder(9)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [MemoryPackOrder(10)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MemoryPackOrder(11)]
    public string? FailureReason { get; set; }

    [MemoryPackOrder(12)]
    public DateTime? CapturedAt { get; set; }

    [MemoryPackOrder(13)]
    public decimal RefundedAmount { get; set; }

    [MemoryPackIgnore]
    public virtual PosSale Sale { get; set; } = null!;
}

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Rest,
    Actions = EndpointActions.None,
    RoutePrefix = "api/pos/returns",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "pos-returns"
)]
public partial class PosReturn : BaseModel
{
    [MemoryPackOrder(0)]
    public string ReturnNumber { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public Guid SaleId { get; set; }

    [MemoryPackOrder(2)]
    public Guid RegisterId { get; set; }

    [MemoryPackOrder(3)]
    public Guid CashierCredentialId { get; set; }

    [MemoryPackOrder(4)]
    public Guid? CustomerCredentialId { get; set; }

    [MemoryPackOrder(5)]
    public PosReturnStatus Status { get; set; } = PosReturnStatus.Pending;

    [MemoryPackOrder(6)]
    public PosPaymentMethod RefundMethod { get; set; }

    [MemoryPackOrder(7)]
    public decimal SubtotalAmount { get; set; }

    [MemoryPackOrder(8)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(9)]
    public decimal TotalRefundAmount { get; set; }

    [MemoryPackOrder(10)]
    public Guid CurrencyId { get; set; }

    [MemoryPackOrder(11)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(12)]
    public string? Reason { get; set; }

    [MemoryPackOrder(13)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MemoryPackOrder(14)]
    public string? RefundReferenceNumber { get; set; }

    [MemoryPackOrder(15)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(16)]
    public string? FailureReason { get; set; }

    [MemoryPackIgnore]
    public virtual PosSale Sale { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual PosRegister Register { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual ICollection<PosReturnLine> Lines { get; set; } = new List<PosReturnLine>();
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class PosReturnLine : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ReturnId { get; set; }

    [MemoryPackOrder(1)]
    public Guid SaleLineId { get; set; }

    [MemoryPackOrder(2)]
    public Guid ProductId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ProductVariationId { get; set; }

    [MemoryPackOrder(4)]
    public string ProductName { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public string? VariantName { get; set; }

    [MemoryPackOrder(6)]
    public decimal Quantity { get; set; }

    [MemoryPackOrder(7)]
    public decimal UnitPrice { get; set; }

    [MemoryPackOrder(8)]
    public decimal TaxAmount { get; set; }

    [MemoryPackOrder(9)]
    public decimal RefundAmount { get; set; }

    [MemoryPackOrder(10)]
    public Guid WarehouseId { get; set; }

    [MemoryPackOrder(11)]
    public Guid LocationId { get; set; }

    [MemoryPackOrder(12)]
    public Guid? LotId { get; set; }

    [MemoryPackOrder(13)]
    public string? InventoryMovementReferenceNumber { get; set; }

    [MemoryPackOrder(14)]
    public string? FailureReason { get; set; }

    [MemoryPackIgnore]
    public virtual PosReturn Return { get; set; } = null!;

    [MemoryPackIgnore]
    public virtual PosSaleLine SaleLine { get; set; } = null!;
}
