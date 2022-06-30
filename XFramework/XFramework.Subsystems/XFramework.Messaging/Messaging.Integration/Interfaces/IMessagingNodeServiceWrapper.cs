using Messaging.Domin.Generic.Contracts.Requests.Create;
using Messaging.Domin.Generic.Contracts.Requests.Update;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace Messaging.Integration.Interfaces;

public interface IMessagingNodeServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<CmdResponse> ConfirmMessageSent(ConfirmMessageSentRequest request);
}