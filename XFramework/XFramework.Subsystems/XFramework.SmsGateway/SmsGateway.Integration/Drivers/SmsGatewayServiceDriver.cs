using SmsGateway.Domain.Generic.Contracts.Requests.Create;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace SmsGateway.Integration.Drivers;

public class SmsGatewayServiceDriver : DriverBase, ISmsGatewayServiceWrapper
{
    public SmsGatewayServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:SmsGatewayService"));
    }


    public async Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request)
    {
        return await SendAsync<GetPendingSmsMessageListRequest, List<MessageDirectResponse>>(request);
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request)
    {
        return await SendAsync<GetScheduledSmsMessageListRequest, List<MessageDirectResponse>>(request);

    }
}   