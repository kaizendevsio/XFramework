using SmsGateway.Domain.Generic.Contracts.Requests.Create;
using Microsoft.AspNetCore.SignalR.Client;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace SmsGateway.Integration.Interfaces;

public interface ISmsGatewayServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }

    public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request);
    public Task<QueryResponse<List<MessageDirectResponse>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request);
    public Task<QueryResponse<List<MessageDirectResponse>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request);
}
    
  