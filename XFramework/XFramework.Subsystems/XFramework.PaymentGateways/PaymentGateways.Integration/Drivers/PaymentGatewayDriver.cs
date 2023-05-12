using Microsoft.Extensions.Configuration;
using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using PaymentGateways.Integration.Interfaces;
using Wallets.Domain.Generic.Contracts.Requests.Create;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace PaymentGateways.Integration.Drivers;

public class PaymentGatewayDriver : DriverBase, IPaymentGatewayWrapper
{
    public PaymentGatewayDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        Configuration = configuration;
        MessageBusDriver = messageBusDriver;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:PaymentGatewayService"));
    }
    
    public async Task<CmdResponse> CreateWithdrawalTransaction(CreateWithdrawalRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<DepositResponse>> CreateDepositTransaction(CreateDepositRequest request)
    {
        return await SendAsync<CreateDepositRequest,DepositResponse>(request);
    }
}