using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Delete;

public class DeleteLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteLaboratoryRequest, DeleteLaboratoryCmd>(connection, mediator);
    }
}