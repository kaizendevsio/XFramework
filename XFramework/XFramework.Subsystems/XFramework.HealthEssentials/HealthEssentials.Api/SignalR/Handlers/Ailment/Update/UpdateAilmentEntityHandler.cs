using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Ailment.Update;

public class UpdateAilmentEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateAilmentEntityRequest, UpdateAilmentEntityCmd>(connection, mediator);
    }
}