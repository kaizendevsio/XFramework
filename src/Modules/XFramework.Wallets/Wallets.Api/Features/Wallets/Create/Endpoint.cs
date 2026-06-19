using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Create;

/// <summary>
/// Create Wallet endpoint
/// </summary>
public static class CreateWalletEndpoint
{
    [MapPost("/api/wallets", Tags = ["Wallets"],
        Summary = "Create a new wallet",
        Description = "Creates a new wallet for a credential with the specified wallet type. Automatically generates a unique account number.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    [BoltHandler]
    public static async Task<Result<WalletResponse>> Handle(
        CreateWalletRequest request,
        IWalletOperationsService walletService,
        IWalletRequestContextResolver contextResolver,
        IWalletFeatureGateService featureGateService,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request, request.CredentialId);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        var feature = await featureGateService.EnsureEnabledAsync(
            contextResult.Data!.TenantId,
            TenantModuleFeatureKeys.Wallets,
            ct);
        if (!feature.IsSuccess)
        {
            return Result<WalletResponse>.Failure(feature.Message!, feature.StatusCode);
        }

        var result = await walletService.CreateWalletAsync(
            request.CredentialId,
            request.WalletTypeId,
            request.InitialBalance,
            contextResult.Data.TenantId,
            ct);

        if (!result.IsSuccess)
            return Result<WalletResponse>.Failure(result.Message ?? "Wallet creation failed", result.StatusCode);

        return Result<WalletResponse>.Success(WalletResponse.FromWallet(result.Data!));
    }
}
