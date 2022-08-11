using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Create;

public class CreateUnitEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateUnitEntityRequest, CreateUnitEntityCmd>(connection, mediator);
    }
}