using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Api.Services;

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
            .ExcludeFromDescription();
    }

    public static async Task<Results<Ok<List<WalletResponse>>, ProblemHttpResult>> Handle(
        [FromRoute] Guid credentialId,
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromServices] IWalletOperationsService walletService,
        CancellationToken ct)
    {
        var result = await walletService.GetWalletsByCredentialAsync(credentialId, tenantId, ct);

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