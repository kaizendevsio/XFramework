using MediatR;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class DequeueMessagesCmd : CommandBaseEntity, IRequest<CmdResponse<DequeueMessagesCmd>>
{
    public StreamFlowClient Client { get; set; }
}