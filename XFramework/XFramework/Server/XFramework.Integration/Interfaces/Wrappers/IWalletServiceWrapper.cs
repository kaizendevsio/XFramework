using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers;

public interface IWalletServiceWrapper : IXFrameworkService
{
    #region Wallet Entity

    public Task<CmdResponse> CreateWalletEntity(CreateWalletEntityRequest request);
    public Task<QueryResponse<WalletEntityResponse>> GetWalletEntity(GetWalletEntityRequest request);
    public Task<QueryResponse<List<WalletEntityResponse>>> GetWalletEntityList(GetWalletEntityListRequest request);
    public Task<CmdResponse> UpdateWalletEntity(UpdateWalletEntityRequest request);
    public Task<CmdResponse> DeleteWalletEntity(DeleteWalletEntityRequest request);

    #endregion


    #region Wallet

    public Task<CmdResponse> CreateWallet(CreateWalletRequest request);
    public Task<QueryResponse<WalletResponse>> GetWallet(GetWalletRequest request);
    public Task<QueryResponse<List<WalletResponse>>> GetWalletList(GetWalletListRequest request);
    public Task<CmdResponse> UpdateWallet(UpdateWalletRequest request);
    public Task<CmdResponse> DeleteWallet(DeleteWalletRequest request);
    public Task<CmdResponse> DecrementWallet(DecrementWalletRequest request);
    public Task<CmdResponse> IncrementWallet(IncrementWalletRequest request);
    public Task<CmdResponse> TransferWallet(TransferWalletRequest request);
        
    public Task<QueryResponse<WalletDepositResponse>> CreateDepositRequest(CreateWalletDepositRequest request);
    public Task<QueryResponse<WalletDepositResponse>> CreateWithdrawalRequest(CreateWalletWithdrawalRequest request);

    #endregion
}