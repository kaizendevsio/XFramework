using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Ailment.Update;

public class UpdateAilmentTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateAilmentTagRequest, UpdateAilmentTagCmd>(connection, mediator);
    }
}