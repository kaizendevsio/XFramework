using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Update;

public class UpdateTagEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateTagEntityGroupRequest, UpdateTagEntityGroupCmd>(connection, mediator);
    }
}