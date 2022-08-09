using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Create;

public class CreateTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateTagRequest, CreateTagCmd>(connection, mediator);
    }
}