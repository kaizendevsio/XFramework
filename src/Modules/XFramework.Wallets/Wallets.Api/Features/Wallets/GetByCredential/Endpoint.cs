using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.GetByCredential;

/// <summary>
/// Get Wallets by Credential endpoint
/// </summary>
public static class GetWalletsByCredentialEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/wallets/credential/{credentialId:guid}", Handle)
            .WithName("GetWalletsByCredential")
            .WithTags("Wallets")
            .RequireAuthorization()
            .ExcludeFromDescription();
    }

    public static async Task<IResult> Handle(
        [FromRoute] Guid credentialId,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromServices] IWalletRequestContextResolver contextResolver,
        [FromServices] IWalletOperationsService walletService,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(new RequestBase
        {
            Metadata = new RequestMetadata { TenantId = tenantId }
        }, credentialId);
        if (!contextResult.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Invalid wallet tenant context",
                detail: contextResult.Message,
                statusCode: contextResult.StatusCode);
        }

        var result = await walletService.GetWalletsByCredentialAsync(credentialId, contextResult.Data!.TenantId, ct);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Error retrieving wallets",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        var response = result.Data!.Select(WalletResponse.FromWallet).ToList();
        return TypedResults.Ok(response);
    }
}
