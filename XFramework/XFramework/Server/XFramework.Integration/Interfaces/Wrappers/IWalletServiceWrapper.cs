using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IWalletServiceWrapper : IXFrameworkService
    {
        #region Wallet Entity

        public Task<CmdResponseBO> CreateWalletEntity(CreateWalletEntityRequest request);
        public Task<QueryResponseBO<WalletEntityResponse>> GetWalletEntity(GetWalletEntityRequest request);
        public Task<QueryResponseBO<List<WalletEntityResponse>>> GetWalletEntityList(GetWalletEntityListRequest request);
        public Task<CmdResponseBO> UpdateWalletEntity(UpdateWalletEntityRequest request);
        public Task<CmdResponseBO> DeleteWalletEntity(DeleteWalletEntityRequest request);

        #endregion


        #region Wallet

        public Task<CmdResponseBO> CreateWallet(CreateWalletRequest request);
        public Task<QueryResponseBO<WalletResponse>> GetWallet(GetWalletRequest request);
        public Task<QueryResponseBO<List<WalletResponse>>> GetWalletList(GetWalletListRequest request);
        public Task<CmdResponseBO> UpdateWallet(UpdateWalletRequest request);
        public Task<CmdResponseBO> DeleteWallet(DeleteWalletRequest request);
        public Task<CmdResponseBO> DecrementWallet(DecrementWalletRequest request);
        public Task<CmdResponseBO> IncrementWallet(IncrementWalletRequest request);
        public Task<CmdResponseBO> TransferWallet(TransferWalletRequest request);
        
        public Task<QueryResponseBO<WalletDepositResponse>> CreateDepositRequest(CreateWalletDepositRequest request);
        public Task<QueryResponseBO<WalletDepositResponse>> CreateWithdrawalRequest(CreateWalletWithdrawalRequest request);

        #endregion
    }
}