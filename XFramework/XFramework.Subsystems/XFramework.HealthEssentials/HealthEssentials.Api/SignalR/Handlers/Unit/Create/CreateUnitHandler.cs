using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Create;

public class CreateUnitHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateUnitRequest, CreateUnitCmd>(connection, mediator);
    }
}