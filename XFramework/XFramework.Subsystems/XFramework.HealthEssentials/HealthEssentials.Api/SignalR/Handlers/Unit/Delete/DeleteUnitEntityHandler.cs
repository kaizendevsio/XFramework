using HealthEssentials.Core.DataAccess.Commands.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Delete;

public class DeleteUnitEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteUnitEntityRequest, DeleteUnitEntityCmd>(connection, mediator);
    }
}