using Wallets.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Integration.Drivers;

public partial interface IWalletsServiceWrapper
{
    public Task<CmdResponse> TransferWallet(TransferWalletRequest request);
    public Task<CmdResponse> ReleaseTransaction(ReleaseTransactionRequest request);
}

public partial record WalletsServiceWrapper
{
    public Task<CmdResponse> TransferWallet(TransferWalletRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ReleaseTransaction(ReleaseTransactionRequest request)
    {
        return SendVoidAsync(request);
    }
}