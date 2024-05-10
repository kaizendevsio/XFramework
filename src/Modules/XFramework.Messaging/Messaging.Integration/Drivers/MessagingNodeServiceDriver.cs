using System.Reflection;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Messaging.Integration.Drivers;

public interface IMessagingNodeServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<CmdResponse> ConfirmMessageSent(UpdateMessageDirectRequest request);
}

public record MessagingNodeServiceDriver : DriverBase, IMessagingNodeServiceWrapper
{
    public MessagingNodeServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();
        
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = serviceId;
    }
    
    public async Task<CmdResponse> ConfirmMessageSent(UpdateMessageDirectRequest request)
    {
        return await SendVoidAsync(request);
    }
}