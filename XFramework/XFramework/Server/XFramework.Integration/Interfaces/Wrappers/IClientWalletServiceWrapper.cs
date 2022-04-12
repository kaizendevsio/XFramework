using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers;

public interface IClientWalletServiceWrapper : IXFrameworkService
{
    #region Wallet Entity

    public Task<QueryResponse<WalletEntityResponse>> GetWalletEntity(GetWalletEntityRequest request);
    public Task<QueryResponse<List<WalletEntityResponse>>> GetWalletEntityList(GetWalletEntityListRequest request);

    #endregion


    #region Wallet

    public Task<QueryResponse<WalletResponse>> GetWallet(GetWalletRequest request);
    public Task<QueryResponse<List<WalletResponse>>> GetWalletList(GetWalletListRequest request);
        
    public Task<QueryResponse<WalletDepositResponse>> CreateDepositRequest(CreateWalletDepositRequest request);
    public Task<QueryResponse<WalletDepositResponse>> CreateWithdrawalRequest(CreateWalletWithdrawalRequest request);

    #endregion
}