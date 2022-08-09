using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Update;

public class UpdateTagEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateTagEntityRequest, UpdateTagEntityCmd>(connection, mediator);
    }
}