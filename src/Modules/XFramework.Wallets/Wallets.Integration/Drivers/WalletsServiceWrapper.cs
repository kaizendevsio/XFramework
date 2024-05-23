using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;

namespace Wallets.Integration.Drivers;

public partial interface IWalletsServiceWrapper
{
    public Task<CmdResponse> IncrementWallet(IncrementWalletRequest request);
    public Task<CmdResponse> DecrementWallet(DecrementWalletRequest request);
    public Task<CmdResponse> TransferWallet(TransferWalletRequest request);
    public Task<CmdResponse> ConvertWallet(ConvertWalletRequest request);
    public Task<CmdResponse> ReleaseTransaction(ReleaseTransactionRequest request);
}

public partial record WalletsServiceWrapper
{
    public Task<CmdResponse> IncrementWallet(IncrementWalletRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> DecrementWallet(DecrementWalletRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> TransferWallet(TransferWalletRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ConvertWallet(ConvertWalletRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ReleaseTransaction(ReleaseTransactionRequest request)
    {
        return SendVoidAsync(request);
    }
}