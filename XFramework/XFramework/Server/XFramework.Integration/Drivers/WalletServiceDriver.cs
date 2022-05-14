using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using Wallets.Domain.Generic.Contracts.Responses;

namespace XFramework.Integration.Drivers;

public class WalletServiceDriver : DriverBase, IWalletServiceWrapper, IClientWalletServiceWrapper
{
    public WalletServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:WalletService"));
    }

    public async Task<QueryResponse<WalletEntityResponse>> GetWalletEntity(GetWalletEntityRequest request)
    {
        return await SendAsync<GetWalletEntityRequest, WalletEntityResponse>(request);
    }

    public async Task<QueryResponse<List<WalletEntityResponse>>> GetWalletEntityList(GetWalletEntityListRequest request)
    {
        return await SendAsync<GetWalletEntityListRequest, List<WalletEntityResponse>>(request);
    }
        
    public async Task<CmdResponse> CreateWalletEntity(CreateWalletEntityRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> UpdateWalletEntity(UpdateWalletEntityRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> DeleteWalletEntity(DeleteWalletEntityRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<QueryResponse<WalletResponse>> GetWallet(GetWalletRequest request)
    {
        return await SendAsync<GetWalletRequest, WalletResponse>(request);
    }

    public async Task<QueryResponse<List<WalletResponse>>> GetWalletList(GetWalletListRequest request)
    {
        return await SendAsync<GetWalletListRequest, List<WalletResponse>>(request);
    }

    public async Task<CmdResponse> CreateWallet(CreateWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> UpdateWallet(UpdateWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> DeleteWallet(DeleteWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> DecrementWallet(DecrementWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> IncrementWallet(IncrementWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    } 

    public async Task<CmdResponse> TransferWallet(TransferWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<CmdResponse> ConvertWallet(ConvertWalletRequest request)
    {
        var result = await SendVoidAsync(request);
        return result.Adapt<CmdResponse>();
    }

    public async Task<QueryResponse<WalletDepositResponse>> CreateDepositRequest(CreateWalletDepositRequest request)
    {
        return await SendAsync<CreateWalletDepositRequest, WalletDepositResponse>(request);
    }

    public async Task<QueryResponse<WalletDepositResponse>> CreateWithdrawalRequest(CreateWalletWithdrawalRequest request)
    {
        return await SendAsync<CreateWalletWithdrawalRequest, WalletDepositResponse>(request);
    }
}