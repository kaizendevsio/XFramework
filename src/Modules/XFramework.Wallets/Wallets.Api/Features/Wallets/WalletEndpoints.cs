using Wallets.Api.Features.Wallets.AddFunds;
using Wallets.Api.Features.Wallets.Convert;
using Wallets.Api.Features.Wallets.Create;
using Wallets.Api.Features.Wallets.Get;
using Wallets.Api.Features.Wallets.GetByCredential;
using Wallets.Api.Features.Wallets.ReleaseTransaction;
using Wallets.Api.Features.Wallets.Transfer;
using Wallets.Api.Features.Wallets.WithdrawFunds;

namespace Wallets.Api.Features.Wallets;

/// <summary>
/// Extension methods for registering Wallet endpoints
/// </summary>
public static class WalletEndpoints
{
    /// <summary>
    /// Maps all Wallet endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wallets")
            .WithTags("Wallets")
            .WithOpenApi();

        // Map individual endpoints
        app.MapCreateWallet();
        app.MapGetWallet();
        app.MapGetWalletsByCredential();
        app.MapAddFunds();
        app.MapWithdrawFunds();
        app.MapTransfer();
        app.MapConvert();
        app.MapReleaseTransaction();

        return app;
    }
}