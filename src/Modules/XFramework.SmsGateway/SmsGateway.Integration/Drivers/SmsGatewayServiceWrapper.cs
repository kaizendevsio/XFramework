using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using Microsoft.Extensions.Configuration;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace SmsGateway.Integration.Drivers;

public partial interface ISmsGatewayServiceWrapper : IServiceWrapper
{
    public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request);

    public Task<QueryResponse<List<SmsNodeJob>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request);

    public Task<QueryResponse<List<SmsNodeJob>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request);
}

public partial record SmsGatewayServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), ISmsGatewayServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "XFramework.SmsGateway".ToSha256();
    }

    public async Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<List<SmsNodeJob>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request)
    {
        return await SendAsync<GetPendingSmsMessageListRequest, List<SmsNodeJob>>(request);
    }

    public async Task<QueryResponse<List<SmsNodeJob>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request)
    {
        return await SendAsync<GetScheduledSmsMessageListRequest, List<SmsNodeJob>>(request);
    }
}
