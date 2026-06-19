using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.Get;

/// <summary>
/// Get Wallet endpoint
/// </summary>
public static class GetWalletEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/{walletId:guid}", Handle)
            .WithName("GetWallet")
            .WithTags("Wallets")
            .RequireAuthorization()
            .ExcludeFromDescription();
    }

    public static async Task<IResult> Handle(
        [FromRoute] Guid walletId,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromServices] IWalletRequestContextResolver contextResolver,
        [FromServices] IWalletOperationsService walletService,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(new RequestBase
        {
            Metadata = new RequestMetadata { TenantId = tenantId }
        });
        if (!contextResult.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Invalid wallet tenant context",
                detail: contextResult.Message,
                statusCode: contextResult.StatusCode);
        }

        var result = await walletService.GetWalletAsync(walletId, contextResult.Data!.TenantId, ct);

        if (!result.IsSuccess)
        {
            if (result.StatusCode == 404)
                return TypedResults.NotFound();

            return TypedResults.Problem(
                title: "Error retrieving wallet",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        var response = WalletResponse.FromWallet(result.Data!);
        return TypedResults.Ok(response);
    }
}
