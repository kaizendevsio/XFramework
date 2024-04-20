using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using Microsoft.AspNetCore.SignalR.Client;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Integration.Interfaces;

public interface ISmsGatewayServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }

    public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request);
    public Task<QueryResponse<List<MessageDirectResponse>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request);
    public Task<QueryResponse<List<MessageDirectResponse>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request);
}
    
  