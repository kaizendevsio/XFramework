using Messaging.Domain.Shared;
using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions.Wrappers;

namespace SmsGateway.Core.Commands.Sms;

public record CreateMessageReceived : RequestBase, ICommand
{
    public string? Sender { get; set; }
    public string Message { get; set; } = null!;
    public string? SubscriptionId { get; set; }
    public string? ReceivedAt { get; set; }
    public Guid AgentClusterId { get; set; }
}


public class CreateMessageReceivedHandler(
    IMessagingServiceWrapper messagingServiceWrapper,
    IMessageBusWrapper messageBusWrapper
    ) : IRequestHandler<CreateMessageReceived, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateMessageReceived request, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var result = await messagingServiceWrapper.MessageDirect
                .Create(new()
                {
                    ExternalSender = request.Sender,
                    Message = request.Message,
                    SubscriptionId = request.SubscriptionId,
                    RecievedAt = string.IsNullOrEmpty(request.ReceivedAt)
                        ? null
                        : DateTime.Parse(request.ReceivedAt).ToUniversalTime(),
                    AgentClusterId = request.AgentClusterId
                });

            if (result.IsSuccess is false)
            {
                return; // Do not publish if failed
            }
            
            var publishObject = new PublishRequest<MessageDirect>(result.Response);
            
            await messageBusWrapper.PublishAsync(
                eventName: MessageEvents.SmsReceived,
                topic: $"agentCluster:{request.AgentClusterId}",
                data: publishObject);
            
        }, CancellationToken.None);
       
        return new()
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Success"
        };
    }
}