using StreamFlow.Domain.Shared.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class DequeueMessagesCmd : CommandBaseEntity, IRequest<CmdResponse<DequeueMessagesCmd>>
{
    public StreamFlowClient Client { get; set; }
}