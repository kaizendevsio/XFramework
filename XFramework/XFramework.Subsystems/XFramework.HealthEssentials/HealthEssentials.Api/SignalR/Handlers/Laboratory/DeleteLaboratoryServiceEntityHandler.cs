using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class DeleteLaboratoryServiceEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteLaboratoryServiceEntityRequest, DeleteLaboratoryServiceEntityCmd>(connection, mediator);
    }
}