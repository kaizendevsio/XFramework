using StreamFlow.Domain.Shared.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity.Events;

public class RegisterClientCmd : CommandBaseEntity, IRequest<CmdResponse<RegisterClientCmd>>
{
    public StreamFlowClient Client { get; set; }
}