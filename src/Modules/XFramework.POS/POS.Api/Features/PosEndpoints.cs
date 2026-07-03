using POS.Api.Services;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace POS.Api.Features;

public static class SearchPosCatalogEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/catalog", Tags = ["POS Catalog"],
        Summary = "Search POS catalog",
        Description = "Delegates to Inventario sellable product search for POS line selection.")]
    public static Task<Result<List<PosCatalogItemResponse>>> Handle(
        SearchPosCatalogRequest request,
        PosCatalogService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class GetPosRegisterEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/registers/{id:guid}", Tags = ["POS Registers"],
        Summary = "Get POS register")]
    public static Task<Result<PosRegisterResponse>> Handle(
        GetPosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class CreatePosRegisterEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/registers", Tags = ["POS Registers"],
        Summary = "Create POS register")]
    public static Task<Result<PosRegisterResponse>> Handle(
        CreatePosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class UpdatePosRegisterEndpoint
{
    [BoltHandler]
    [MapPut("/api/pos/registers/{id:guid}", Tags = ["POS Registers"],
        Summary = "Update POS register")]
    public static Task<Result<PosRegisterResponse>> Handle(
        UpdatePosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.UpdateAsync(request, ct);
}

public static class CreatePosCartEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/carts", Tags = ["POS Carts"],
        Summary = "Create POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        CreatePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class UpdatePosCartEndpoint
{
    [BoltHandler]
    [MapPut("/api/pos/carts/{id:guid}", Tags = ["POS Carts"],
        Summary = "Update POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        UpdatePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.UpdateAsync(request, ct);
}

public static class GetPosCartEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/carts/{id:guid}", Tags = ["POS Carts"],
        Summary = "Get POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        GetPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosCartsEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/carts", Tags = ["POS Carts"],
        Summary = "Search POS carts")]
    public static Task<Result<List<PosCartSummaryResponse>>> Handle(
        SearchPosCartsRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class SuspendPosCartEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/carts/{cartId:guid}/suspend", Tags = ["POS Carts"],
        Summary = "Suspend POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        SuspendPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.SuspendAsync(request, ct);
}

public static class ResumePosCartEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/carts/{cartId:guid}/resume", Tags = ["POS Carts"],
        Summary = "Resume POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        ResumePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.ResumeAsync(request, ct);
}

public static class CancelPosCartEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/carts/{cartId:guid}/cancel", Tags = ["POS Carts"],
        Summary = "Cancel POS cart")]
    public static Task<Result<PosCartResponse>> Handle(
        CancelPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CancelAsync(request, ct);
}

public static class CheckoutPosCartEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/carts/{cartId:guid}/checkout", Tags = ["POS Carts"],
        Summary = "Checkout POS cart",
        Description = "Converts a persisted POS cart into a sale through the existing checkout orchestration.")]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CheckoutPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CheckoutAsync(request, ct);
}

public static class CheckoutPosSaleEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/sales/checkout", Tags = ["POS Sales"],
        Summary = "Checkout POS sale",
        Description = "Creates a sale, reserves Inventario stock, captures Wallets payment, and fulfills reservations.")]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CheckoutPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.CheckoutAsync(request, ct);
}

public static class GetPosSaleEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/sales/{id:guid}", Tags = ["POS Sales"],
        Summary = "Get POS sale")]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        GetPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosSalesEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/sales", Tags = ["POS Sales"],
        Summary = "Search POS sales")]
    public static Task<Result<List<PosSaleSummaryResponse>>> Handle(
        SearchPosSalesRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class CancelPosSaleEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/sales/{saleId:guid}/cancel", Tags = ["POS Sales"],
        Summary = "Cancel POS sale")]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CancelPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.CancelAsync(request, ct);
}

public static class RetryPosSaleFulfillmentEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/sales/{saleId:guid}/retry-fulfillment", Tags = ["POS Sales"],
        Summary = "Retry POS sale fulfillment")]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        RetryPosSaleFulfillmentRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.RetryFulfillmentAsync(request, ct);
}

public static class CreatePosReturnEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/returns", Tags = ["POS Returns"],
        Summary = "Create POS return",
        Description = "Posts returned stock through Inventario and refunds through Wallets.")]
    public static Task<Result<PosReturnResponse>> Handle(
        CreatePosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class GetPosReturnEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/returns/{id:guid}", Tags = ["POS Returns"],
        Summary = "Get POS return")]
    public static Task<Result<PosReturnResponse>> Handle(
        GetPosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosReturnsEndpoint
{
    [BoltHandler]
    [MapGet("/api/pos/returns", Tags = ["POS Returns"],
        Summary = "Search POS returns")]
    public static Task<Result<List<PosReturnSummaryResponse>>> Handle(
        SearchPosReturnsRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class RetryPosReturnEndpoint
{
    [BoltHandler]
    [MapPost("/api/pos/returns/{returnId:guid}/retry", Tags = ["POS Returns"],
        Summary = "Retry POS return",
        Description = "Retries recoverable inventory posting or refund failures for a POS return.")]
    public static Task<Result<PosReturnResponse>> Handle(
        RetryPosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.RetryAsync(request, ct);
}
