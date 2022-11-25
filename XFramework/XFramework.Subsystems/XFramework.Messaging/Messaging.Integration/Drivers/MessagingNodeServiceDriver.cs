using Messaging.Domain.Generic.Contracts.Requests.Update;
using Messaging.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace Messaging.Integration.Drivers;

public class MessagingNodeServiceDriver : DriverBase, IMessagingNodeServiceWrapper
{
    public MessagingNodeServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:MessagingService"));
    }
    
    public async Task<CmdResponse> ConfirmMessageSent(ConfirmMessageSentRequest request)
    {
        return await SendVoidAsync(request);
    }
}