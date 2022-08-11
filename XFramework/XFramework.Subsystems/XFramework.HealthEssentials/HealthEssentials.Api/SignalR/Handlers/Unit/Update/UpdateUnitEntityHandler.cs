using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Update;

public class UpdateUnitEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateUnitEntityRequest, UpdateUnitEntityCmd>(connection, mediator);
    }
}