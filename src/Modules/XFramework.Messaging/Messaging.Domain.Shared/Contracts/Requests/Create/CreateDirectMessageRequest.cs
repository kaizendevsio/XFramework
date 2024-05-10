using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateDirectMessageRequest;
using TResponse = CmdResponse;

public record CreateDirectMessageRequest : RequestBase,
    ICommand, 
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
    public Guid? MessageTypeId { get; set; }
    public DateTime? SendSchedule { get; set; }
    public required MessageTransportType MessageTransportType { get; set; }
    public string Sender { get; set; } = "System";
    public required string Recipient { get; set; } = null!;
    public string? Subject { get; set; }
    public string Intent { get; set; } = "Notification";
    public required string Message { get; set; } = null!;
    public bool IsScheduled { get; set; }
}