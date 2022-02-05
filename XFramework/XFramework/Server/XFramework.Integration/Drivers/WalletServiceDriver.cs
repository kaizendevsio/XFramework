using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using Wallets.Domain.Generic.Contracts.Responses;

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

        public async Task<QueryResponseBO<WalletEntityResponse>> GetWalletEntity(GetWalletEntityRequest request)
        {
            return await SendAsync<GetWalletEntityRequest, WalletEntityResponse>("GetWalletEntity", request);
        }

        public async Task<QueryResponseBO<List<WalletEntityResponse>>> GetWalletEntityList(GetWalletEntityListRequest request)
        {
            return await SendAsync<GetWalletEntityListRequest, List<WalletEntityResponse>>("GetWalletEntityList", request);
        }
        
        public async Task<CmdResponseBO> CreateWalletEntity(CreateWalletEntityRequest request)
        {
            var result = await SendAsync<CreateWalletEntityRequest, CmdResponseBO>("CreateEntityWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> UpdateWalletEntity(UpdateWalletEntityRequest request)
        {
            var result = await SendAsync<UpdateWalletEntityRequest, CmdResponseBO>("UpdateWalletEntity", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> DeleteWalletEntity(DeleteWalletEntityRequest request)
        {
            var result = await SendAsync<DeleteWalletEntityRequest, CmdResponseBO>("DeleteWalletEntity", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<QueryResponseBO<WalletResponse>> GetWallet(GetWalletRequest request)
        {
          return await SendAsync<GetWalletRequest, WalletResponse>("GetWallet", request);
        }

        public async Task<QueryResponseBO<List<WalletResponse>>> GetWalletList(GetWalletListRequest request)
        {
           return await SendAsync<GetWalletListRequest, List<WalletResponse>>("GetWalletList", request);
        }

        public async Task<CmdResponseBO> CreateWallet(CreateWalletRequest request)
        {
            var result = await SendAsync<CreateWalletRequest, CmdResponseBO>("CreateWallet", request);
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

        public async Task<CmdResponseBO> DecrementWallet(DecrementWalletRequest request)
        {
            var result = await SendAsync<DecrementWalletRequest, CmdResponseBO>("DecrementWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<CmdResponseBO> IncrementWallet(IncrementWalletRequest request)
        {
            var result = await SendAsync<IncrementWalletRequest, CmdResponseBO>("IncrementWallet", request);
            return result.Adapt<CmdResponseBO>();
        } 

        public async Task<CmdResponseBO> TransferWallet(TransferWalletRequest request)
        {
            var result = await SendAsync<TransferWalletRequest, CmdResponseBO>("TransferWallet", request);
            return result.Adapt<CmdResponseBO>();
        }

        public async Task<QueryResponseBO<WalletDepositResponse>> CreateDepositRequest(CreateWalletDepositRequest request)
        {
            return await SendAsync<CreateWalletDepositRequest, WalletDepositResponse>("CreateWalletDeposit", request);
        }

        public async Task<QueryResponseBO<WalletDepositResponse>> CreateWithdrawalRequest(CreateWalletWithdrawalRequest request)
        {
            return await SendAsync<CreateWalletWithdrawalRequest, WalletDepositResponse>("WithdrawalWallet", request);
        }
    }
}