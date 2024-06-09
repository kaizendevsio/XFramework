using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.Contracts.Requests;

namespace SmsGateway.Core.Commands.Sms;

public record CreateMessageReceived : RequestBase, ICommand
{
    public string? Sender { get; set; }
    public string Message { get; set; } = null!;
    public string? SubscriptionId { get; set; }
    public string? ReceivedAt { get; set; }
    public Guid AgentClusterId { get; set; }
}


public class CreateMessageReceivedHandler(IMessagingServiceWrapper messagingServiceWrapper) : IRequestHandler<CreateMessageReceived, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateMessageReceived request, CancellationToken cancellationToken)
    {
        _ = messagingServiceWrapper.MessageDirect
            .Create(new()
            {
                ExternalSender = request.Sender,
                Message = request.Message,
                SubscriptionId = request.SubscriptionId,
                RecievedAt = string.IsNullOrEmpty(request.ReceivedAt) ? null : DateTime.Parse(request.ReceivedAt).ToUniversalTime(),
                AgentClusterId = request.AgentClusterId
            });

        return new()
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Success"
        };
    }
}