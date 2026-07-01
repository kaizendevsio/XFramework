namespace POS.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record SearchPosCatalogRequest : RequestBase,
    IQuery<QueryResponse<List<PosCatalogItemResponse>>>,
    IBoltRequest<SearchPosCatalogRequest, QueryResponse<List<PosCatalogItemResponse>>>
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsAvailable { get; set; } = true;
    public bool IncludeBaseProducts { get; set; } = true;
    public bool IncludeVariants { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

[MemoryPackable]
public partial record GetPosRegisterRequest : RequestBase,
    IQuery<QueryResponse<PosRegisterResponse>>,
    IBoltRequest<GetPosRegisterRequest, QueryResponse<PosRegisterResponse>>
{
    public Guid Id { get; set; }
}

[MemoryPackable]
public partial record CreatePosRegisterRequest : RequestBase,
    ICommand<CmdResponse<PosRegisterResponse>>,
    IBoltRequest<CreatePosRegisterRequest, CmdResponse<PosRegisterResponse>>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Guid MerchantCredentialId { get; set; }
    public Guid CashDrawerWalletId { get; set; }
    public Guid WalletTypeId { get; set; }
    public Guid CurrencyId { get; set; }
    public Guid DefaultWarehouseId { get; set; }
    public Guid DefaultLocationId { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

[MemoryPackable]
public partial record UpdatePosRegisterRequest : RequestBase,
    ICommand<CmdResponse<PosRegisterResponse>>,
    IBoltRequest<UpdatePosRegisterRequest, CmdResponse<PosRegisterResponse>>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Guid MerchantCredentialId { get; set; }
    public Guid CashDrawerWalletId { get; set; }
    public Guid WalletTypeId { get; set; }
    public Guid CurrencyId { get; set; }
    public Guid DefaultWarehouseId { get; set; }
    public Guid DefaultLocationId { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

[MemoryPackable]
public partial record CreatePosCartRequest : RequestBase,
    ICommand<CmdResponse<PosCartResponse>>,
    IBoltRequest<CreatePosCartRequest, CmdResponse<PosCartResponse>>
{
    public Guid RegisterId { get; set; }
    public Guid CashierCredentialId { get; set; }
    public Guid? CustomerCredentialId { get; set; }
    public string? CustomerLabel { get; set; }
    public string? Notes { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CurrencyId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool Suspend { get; set; }
    public List<PosCartLineRequest> Lines { get; set; } = [];
}

[MemoryPackable]
public partial record UpdatePosCartRequest : RequestBase,
    ICommand<CmdResponse<PosCartResponse>>,
    IBoltRequest<UpdatePosCartRequest, CmdResponse<PosCartResponse>>
{
    public Guid Id { get; set; }
    public Guid? CustomerCredentialId { get; set; }
    public string? CustomerLabel { get; set; }
    public string? Notes { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CurrencyId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? ExpectedConcurrencyStamp { get; set; }
    public List<PosCartLineRequest> Lines { get; set; } = [];
}

[MemoryPackable]
public partial record PosCartLineRequest
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? ExpectedUnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? LotId { get; set; }
}

[MemoryPackable]
public partial record GetPosCartRequest : RequestBase,
    IQuery<QueryResponse<PosCartResponse>>,
    IBoltRequest<GetPosCartRequest, QueryResponse<PosCartResponse>>
{
    public Guid Id { get; set; }
}

[MemoryPackable]
public partial record SearchPosCartsRequest : RequestBase,
    IQuery<QueryResponse<List<PosCartSummaryResponse>>>,
    IBoltRequest<SearchPosCartsRequest, QueryResponse<List<PosCartSummaryResponse>>>
{
    public Guid? RegisterId { get; set; }
    public Guid? CashierCredentialId { get; set; }
    public PosCartStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool IncludeExpired { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

[MemoryPackable]
public partial record SuspendPosCartRequest : RequestBase,
    ICommand<CmdResponse<PosCartResponse>>,
    IBoltRequest<SuspendPosCartRequest, CmdResponse<PosCartResponse>>
{
    public Guid CartId { get; set; }
    public Guid? ExpectedConcurrencyStamp { get; set; }
}

[MemoryPackable]
public partial record ResumePosCartRequest : RequestBase,
    ICommand<CmdResponse<PosCartResponse>>,
    IBoltRequest<ResumePosCartRequest, CmdResponse<PosCartResponse>>
{
    public Guid CartId { get; set; }
    public Guid? ExpectedConcurrencyStamp { get; set; }
}

[MemoryPackable]
public partial record CancelPosCartRequest : RequestBase,
    ICommand<CmdResponse<PosCartResponse>>,
    IBoltRequest<CancelPosCartRequest, CmdResponse<PosCartResponse>>
{
    public Guid CartId { get; set; }
    public string? Reason { get; set; }
    public Guid? ExpectedConcurrencyStamp { get; set; }
}

[MemoryPackable]
public partial record CheckoutPosCartRequest : RequestBase,
    ICommand<CmdResponse<PosSaleReceiptResponse>>,
    IBoltRequest<CheckoutPosCartRequest, CmdResponse<PosSaleReceiptResponse>>
{
    public Guid CartId { get; set; }
    public Guid? ExpectedConcurrencyStamp { get; set; }
    public string? IdempotencyKey { get; set; }
    public CheckoutPosPaymentRequest Payment { get; set; } = new();
}

[MemoryPackable]
public partial record CheckoutPosSaleRequest : RequestBase,
    ICommand<CmdResponse<PosSaleReceiptResponse>>,
    IBoltRequest<CheckoutPosSaleRequest, CmdResponse<PosSaleReceiptResponse>>
{
    public Guid RegisterId { get; set; }
    public Guid CashierCredentialId { get; set; }
    public Guid? CustomerCredentialId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CurrencyId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? IdempotencyKey { get; set; }
    public List<CheckoutPosSaleLineRequest> Lines { get; set; } = [];
    public CheckoutPosPaymentRequest Payment { get; set; } = new();
}

[MemoryPackable]
public partial record CheckoutPosSaleLineRequest
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ExpectedUnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? LotId { get; set; }
    public string? UnitOfMeasure { get; set; }
}

[MemoryPackable]
public partial record CheckoutPosPaymentRequest
{
    public PosPaymentMethod Method { get; set; } = PosPaymentMethod.CashDrawer;
    public decimal Amount { get; set; }
    public Guid? CustomerCredentialId { get; set; }
}

[MemoryPackable]
public partial record GetPosSaleRequest : RequestBase,
    IQuery<QueryResponse<PosSaleReceiptResponse>>,
    IBoltRequest<GetPosSaleRequest, QueryResponse<PosSaleReceiptResponse>>
{
    public Guid Id { get; set; }
}

[MemoryPackable]
public partial record SearchPosSalesRequest : RequestBase,
    IQuery<QueryResponse<List<PosSaleSummaryResponse>>>,
    IBoltRequest<SearchPosSalesRequest, QueryResponse<List<PosSaleSummaryResponse>>>
{
    public Guid? RegisterId { get; set; }
    public PosSaleStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

[MemoryPackable]
public partial record CancelPosSaleRequest : RequestBase,
    ICommand<CmdResponse<PosSaleReceiptResponse>>,
    IBoltRequest<CancelPosSaleRequest, CmdResponse<PosSaleReceiptResponse>>
{
    public Guid SaleId { get; set; }
    public string? Reason { get; set; }
}

[MemoryPackable]
public partial record RetryPosSaleFulfillmentRequest : RequestBase,
    ICommand<CmdResponse<PosSaleReceiptResponse>>,
    IBoltRequest<RetryPosSaleFulfillmentRequest, CmdResponse<PosSaleReceiptResponse>>
{
    public Guid SaleId { get; set; }
}

[MemoryPackable]
public partial record CreatePosReturnRequest : RequestBase,
    ICommand<CmdResponse<PosReturnResponse>>,
    IBoltRequest<CreatePosReturnRequest, CmdResponse<PosReturnResponse>>
{
    public Guid SaleId { get; set; }
    public Guid CashierCredentialId { get; set; }
    public PosPaymentMethod RefundMethod { get; set; } = PosPaymentMethod.CashDrawer;
    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }
    public List<CreatePosReturnLineRequest> Lines { get; set; } = [];
}

[MemoryPackable]
public partial record CreatePosReturnLineRequest
{
    public Guid SaleLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal TaxAmount { get; set; }
}

[MemoryPackable]
public partial record GetPosReturnRequest : RequestBase,
    IQuery<QueryResponse<PosReturnResponse>>,
    IBoltRequest<GetPosReturnRequest, QueryResponse<PosReturnResponse>>
{
    public Guid Id { get; set; }
}

[MemoryPackable]
public partial record SearchPosReturnsRequest : RequestBase,
    IQuery<QueryResponse<List<PosReturnSummaryResponse>>>,
    IBoltRequest<SearchPosReturnsRequest, QueryResponse<List<PosReturnSummaryResponse>>>
{
    public Guid? SaleId { get; set; }
    public Guid? RegisterId { get; set; }
    public PosReturnStatus? Status { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
