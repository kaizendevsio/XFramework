namespace SmsGateway.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateSmsMessageRequest;
using TResponse = CmdResponse;

public record CreateSmsMessageRequest : RequestBase,
    ICommand,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid Id { get; set; }
    public Guid AgentClusterId { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Intent { get; set; }
    public string? Message { get; set; }
    public DateTime? SendSchedule { get; set; }
    public bool IsScheduled { get; set; }
}