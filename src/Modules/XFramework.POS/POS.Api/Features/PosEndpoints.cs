using POS.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace POS.Api.Features;

public static class SearchPosCatalogEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    [MapGet("/api/pos/catalog", Tags = ["POS Catalog"],
        Summary = "Search POS catalog",
        Description = "Delegates to Inventario sellable product search for POS line selection.",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    public static Task<Result<List<PosCatalogItemResponse>>> Handle(
        SearchPosCatalogRequest request,
        PosCatalogService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class GetPosRegisterEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersView])]
    [MapGet("/api/pos/registers/{id:guid}", Tags = ["POS Registers"],
        Summary = "Get POS register",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersView])]
    public static Task<Result<PosRegisterResponse>> Handle(
        GetPosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class CreatePosRegisterEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersCreate])]
    [MapPost("/api/pos/registers", Tags = ["POS Registers"],
        Summary = "Create POS register",
        Capability = IdentityAuthorizationConstants.Create,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersCreate])]
    public static Task<Result<PosRegisterResponse>> Handle(
        CreatePosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class UpdatePosRegisterEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersUpdate])]
    [MapPut("/api/pos/registers/{id:guid}", Tags = ["POS Registers"],
        Summary = "Update POS register",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.RegistersUpdate])]
    public static Task<Result<PosRegisterResponse>> Handle(
        UpdatePosRegisterRequest request,
        PosRegisterService service,
        CancellationToken ct) =>
        service.UpdateAsync(request, ct);
}

public static class CreatePosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsCreate])]
    [MapPost("/api/pos/carts", Tags = ["POS Carts"],
        Summary = "Create POS cart",
        Capability = IdentityAuthorizationConstants.Create,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsCreate])]
    public static Task<Result<PosCartResponse>> Handle(
        CreatePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class UpdatePosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    [MapPut("/api/pos/carts/{id:guid}", Tags = ["POS Carts"],
        Summary = "Update POS cart",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    public static Task<Result<PosCartResponse>> Handle(
        UpdatePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.UpdateAsync(request, ct);
}

public static class GetPosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsView])]
    [MapGet("/api/pos/carts/{id:guid}", Tags = ["POS Carts"],
        Summary = "Get POS cart",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsView])]
    public static Task<Result<PosCartResponse>> Handle(
        GetPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosCartsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsView])]
    [MapGet("/api/pos/carts", Tags = ["POS Carts"],
        Summary = "Search POS carts",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsView])]
    public static Task<Result<List<PosCartSummaryResponse>>> Handle(
        SearchPosCartsRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class SuspendPosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    [MapPost("/api/pos/carts/{cartId:guid}/suspend", Tags = ["POS Carts"],
        Summary = "Suspend POS cart",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    public static Task<Result<PosCartResponse>> Handle(
        SuspendPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.SuspendAsync(request, ct);
}

public static class ResumePosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    [MapPost("/api/pos/carts/{cartId:guid}/resume", Tags = ["POS Carts"],
        Summary = "Resume POS cart",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate])]
    public static Task<Result<PosCartResponse>> Handle(
        ResumePosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.ResumeAsync(request, ct);
}

public static class CancelPosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsDelete])]
    [MapPost("/api/pos/carts/{cartId:guid}/cancel", Tags = ["POS Carts"],
        Summary = "Cancel POS cart",
        Capability = IdentityAuthorizationConstants.Delete,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsDelete])]
    public static Task<Result<PosCartResponse>> Handle(
        CancelPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CancelAsync(request, ct);
}

public static class CheckoutPosCartEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate, PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/carts/{cartId:guid}/checkout", Tags = ["POS Carts"],
        Summary = "Checkout POS cart",
        Description = "Converts a persisted POS cart into a sale through the existing checkout orchestration.",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.CartsUpdate, PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CheckoutPosCartRequest request,
        PosCartService service,
        CancellationToken ct) =>
        service.CheckoutAsync(request, ct);
}

public static class CheckoutPosSaleEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesCreate])]
    [MapPost("/api/pos/sales/checkout", Tags = ["POS Sales"],
        Summary = "Checkout POS sale",
        Description = "Creates a sale, reserves Inventario stock, captures Wallets payment, and fulfills reservations.",
        Capability = IdentityAuthorizationConstants.Create,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesCreate])]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CheckoutPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.CheckoutAsync(request, ct);
}

public static class GetPosSaleEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    [MapGet("/api/pos/sales/{id:guid}", Tags = ["POS Sales"],
        Summary = "Get POS sale",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        GetPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosSalesEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    [MapGet("/api/pos/sales", Tags = ["POS Sales"],
        Summary = "Search POS sales",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    public static Task<Result<List<PosSaleSummaryResponse>>> Handle(
        SearchPosSalesRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class CancelPosSaleEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesDelete])]
    [MapPost("/api/pos/sales/{saleId:guid}/cancel", Tags = ["POS Sales"],
        Summary = "Cancel POS sale",
        Capability = IdentityAuthorizationConstants.Delete,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesDelete])]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        CancelPosSaleRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.CancelAsync(request, ct);
}

public static class RetryPosSaleFulfillmentEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesUpdate])]
    [MapPost("/api/pos/sales/{saleId:guid}/retry-fulfillment", Tags = ["POS Sales"],
        Summary = "Retry POS sale fulfillment",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesUpdate])]
    public static Task<Result<PosSaleReceiptResponse>> Handle(
        RetryPosSaleFulfillmentRequest request,
        PosSalesService service,
        CancellationToken ct) =>
        service.RetryFulfillmentAsync(request, ct);
}

public static class CreatePosReturnEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsCreate])]
    [MapPost("/api/pos/returns", Tags = ["POS Returns"],
        Summary = "Create POS return",
        Description = "Posts returned stock through Inventario and refunds through Wallets.",
        Capability = IdentityAuthorizationConstants.Create,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsCreate])]
    public static Task<Result<PosReturnResponse>> Handle(
        CreatePosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public static class GetPosReturnEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsView])]
    [MapGet("/api/pos/returns/{id:guid}", Tags = ["POS Returns"],
        Summary = "Get POS return",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsView])]
    public static Task<Result<PosReturnResponse>> Handle(
        GetPosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class SearchPosReturnsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsView])]
    [MapGet("/api/pos/returns", Tags = ["POS Returns"],
        Summary = "Search POS returns",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsView])]
    public static Task<Result<List<PosReturnSummaryResponse>>> Handle(
        SearchPosReturnsRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.SearchAsync(request, ct);
}

public static class RetryPosReturnEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsUpdate])]
    [MapPost("/api/pos/returns/{returnId:guid}/retry", Tags = ["POS Returns"],
        Summary = "Retry POS return",
        Description = "Retries recoverable inventory posting or refund failures for a POS return.",
        Capability = IdentityAuthorizationConstants.Update,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.ReturnsUpdate])]
    public static Task<Result<PosReturnResponse>> Handle(
        RetryPosReturnRequest request,
        PosReturnsService service,
        CancellationToken ct) =>
        service.RetryAsync(request, ct);
}
