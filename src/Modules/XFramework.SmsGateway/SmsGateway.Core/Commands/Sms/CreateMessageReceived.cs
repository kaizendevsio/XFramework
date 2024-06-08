using XFramework.Domain.Shared.Contracts.Requests;

namespace SmsGateway.Core.Commands.Sms;

public record CreateMessageReceived : RequestBase, ICommand
{
    public string? Sender { get; set; }
    public string? Message { get; set; }
    public string? SubscriptionId { get; set; }
    public string? ReceivedAt { get; set; }
    public Guid AgentClusterId { get; set; }
}


public class CreateMessageReceivedHandler : IRequestHandler<CreateMessageReceived, CmdResponse>
{
    public Task<CmdResponse> Handle(CreateMessageReceived request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}