using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Wallets.Domain.Generic.Contracts.Requests.Wallet;
using Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IWalletServiceWrapper : IXFrameworkService
    {
        #region Wallets

        public Task<CmdResponseBO> CreateWallet(CreateWalletRequest request);
        public Task<QueryResponseBO<GetWalletContract>> GetWallet(GetWalletRequest request);
        public Task<QueryResponseBO<List<GetWalletContract>>> GetAllWallet(GetAllWalletRequest request);
        public Task<CmdResponseBO> UpdateWallet(UpdateWalletRequest request);
        public Task<CmdResponseBO> DeleteWallet(DeleteWalletRequest request);

        #endregion


        #region Identity Wallet

        public Task<CmdResponseBO> CreateIdentityWallet(CreateIdentityWalletRequest request);
        public Task<QueryResponseBO<GetIdentityWalletContract>> GetIdentityWallet(GetIdentityWalletRequest request);
        public Task<QueryResponseBO<List<GetIdentityWalletContract>>> GetAllIdentityWallet(GetAllIdentityWalletRequest request);
        public Task<CmdResponseBO> UpdateIdentityWallet(UpdateIdentityWalletRequest request);
        public Task<CmdResponseBO> DeleteIdentityWallet(DeleteIdentityWalletRequest request);
        public Task<CmdResponseBO> DecrementIdentityWallet(DecrementIdentityWalletRequest request);
        public Task<CmdResponseBO> IncrementIdentityWallet(IncrementIdentityWalletRequest request);
        public Task<CmdResponseBO> TransferIdentityWallet(TransferIdentityWalletRequest request);
        
        public Task<QueryResponseBO<WalletDepositContract>> CreateDepositRequest(CreateIdentityWalletDepositRequest request);
        public Task<QueryResponseBO<WalletDepositContract>> CreateWithdrawalRequest(CreateIdentityWalletWithdrawalRequest request);

        #endregion
    }
}