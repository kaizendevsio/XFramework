namespace SmsGateway.Domain.Shared.Contracts.Requests.Create;

/// <summary>
/// Request to create a record of a received SMS message
/// </summary>
public record CreateMessageReceivedRequest : RequestBase
{
    public string? Sender { get; set; }
    public string Message { get; set; } = null!;
    public string? SubscriptionId { get; set; }
    public string? ReceivedAt { get; set; }
    public Guid AgentClusterId { get; set; }
}