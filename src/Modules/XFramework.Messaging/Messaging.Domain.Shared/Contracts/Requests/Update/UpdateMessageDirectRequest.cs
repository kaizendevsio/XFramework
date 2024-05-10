using MediatR;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateMessageDirectRequest : RequestBase,
    ICommand,
    IStreamflowRequest<UpdateMessageDirectRequest, TResponse>
{
    public Guid? Id { get; set; }
}