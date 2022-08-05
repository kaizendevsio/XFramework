using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Ailment.Delete;

public class DeleteAilmentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteAilmentRequest, DeleteAilmentCmd>(connection, mediator);

    }
}