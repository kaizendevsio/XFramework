using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Responses;
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
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId(request);
        if (tenantId == Guid.Empty)
        {
            return Result<WalletResponse>.Failure("Tenant ID is required", 400);
        }

        var result = await walletService.CreateWalletAsync(
            request.CredentialId,
            request.WalletTypeId,
            request.InitialBalance,
            tenantId,
            ct);

        if (!result.IsSuccess)
            return Result<WalletResponse>.Failure(result.Message ?? "Wallet creation failed", result.StatusCode);

        return Result<WalletResponse>.Success(WalletResponse.FromWallet(result.Data!));
    }

    private static Guid ResolveTenantId(CreateWalletRequest request)
    {
        var metadataTenantId = request.Metadata.TenantId.GetValueOrDefault();
        if (metadataTenantId != Guid.Empty)
        {
            return metadataTenantId;
        }

        return request.TenantId.GetValueOrDefault();
    }
}
