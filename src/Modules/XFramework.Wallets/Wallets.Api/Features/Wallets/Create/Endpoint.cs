using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Api.Services;
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
        ExcludeFromOpenApi = true)]
    public static async Task<Result<WalletResponse>> Handle(
        CreateWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        var result = await walletService.CreateWalletAsync(
            request.CredentialId,
            request.WalletTypeId,
            request.InitialBalance,
            request.TenantId,
            ct);

        if (!result.IsSuccess)
            return Result<WalletResponse>.Failure(result.Message, result.StatusCode);

        return Result<WalletResponse>.Success(WalletResponse.FromWallet(result.Data!));
    }
}