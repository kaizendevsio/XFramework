using Messaging.Domin.Generic.Contracts.Requests.Create;
using Messaging.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace Messaging.Integration.Drivers;

public class MessagingServiceDriver : DriverBase, IMessagingServiceWrapper
{
    public MessagingServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:MessagingService"));
    }

    public async Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return await SendVoidAsync("CreateDirectMessage", request);
    }
}   