using MediatR;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateMessageDirectRequest : RequestBase,
    ICommand,
    IStreamflowRequest<UpdateMessageDirectRequest, TResponse>
{
    public Guid? Id { get; set; }
    public Guid AgentClusterId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? RecievedAt { get; set; }
}