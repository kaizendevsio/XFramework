using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using Microsoft.Extensions.Configuration;
using Wallets.Domain.Generic.Contracts.Requests.Wallet;
using Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class WalletServiceDriver : DriverBase, IWalletServiceWrapper
    {
        public WalletServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
        {
            MessageBusDriver = messageBusDriver;
            Configuration = configuration;
            TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:WalletService"));
        }


        public async Task<CmdResponseBO> CreateWallet(CreateWalletRequest request)
        {
            var result = await SendAsync<CreateWalletRequest, CmdResponseBO>("CreateWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> GetWallet(GetWalletRequest request)
        {
            var result = await SendAsync<GetWalletRequest, CmdResponseBO>("GetWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> GetAllWallet(GetAllWalletRequest request)
        {
            var result = await SendAsync<GetAllWalletRequest, CmdResponseBO>("GetAllWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateWallet(UpdateWalletRequest request)
        {
            var result = await SendAsync<UpdateWalletRequest, CmdResponseBO>("UpdateWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteWallet(DeleteWalletRequest request)
        {
            var result = await SendAsync<DeleteWalletRequest, CmdResponseBO>("DeleteWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> CreateIdentityWallet(CreateIdentityWalletRequest request)
        {
            var result = await SendAsync<CreateIdentityWalletRequest, CmdResponseBO>("CreateIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<QueryResponseBO<GetIdentityWalletContract>> GetIdentityWallet(GetIdentityWalletRequest request)
        {
            var result = await SendAsync<GetIdentityWalletRequest, GetIdentityWalletContract>("GetIdentityWallet", request);
            return result;
        }

        public async Task<QueryResponseBO<List<GetIdentityWalletContract>>> GetAllIdentityWallet(GetAllIdentityWalletRequest request)
        {
            var result = await SendAsync<GetAllIdentityWalletRequest, List<GetIdentityWalletContract>>("GetAllIdentityWallet", request);
            return result;
        }

        public async Task<CmdResponseBO> UpdateIdentityWallet(UpdateIdentityWalletRequest request)
        {
            var result = await SendAsync<UpdateIdentityWalletRequest, CmdResponseBO>("UpdateIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteIdentityWallet(DeleteIdentityWalletRequest request)
        {
            var result = await SendAsync<DeleteIdentityWalletRequest, CmdResponseBO>("DeleteIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
            throw new System.NotImplementedException();
        }

        public async Task<CmdResponseBO> DecrementIdentityWallet(DecrementIdentityWalletRequest request)
        {
            var result =
                await SendAsync<DecrementIdentityWalletRequest, CmdResponseBO>("DecrementIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
            throw new System.NotImplementedException();
        }

        public async Task<CmdResponseBO> IncrementIdentityWallet(IncrementIdentityWalletRequest request)
        {
            var result =
                await SendAsync<IncrementIdentityWalletRequest, CmdResponseBO>("IncrementIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
            throw new System.NotImplementedException();
        }

        public async Task<CmdResponseBO> TransferIdentityWallet(TransferIdentityWalletRequest request)
        {
            var result =
                await SendAsync<TransferIdentityWalletRequest, CmdResponseBO>("TransferIdentityWallet", request);
            return result.Adapt<CmdResponseBO>();
            throw new System.NotImplementedException();
        }
    }
}