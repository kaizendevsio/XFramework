using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Create;

public class CreateLaboratoryServiceEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLaboratoryServiceEntityRequest, CreateLaboratoryServiceEntityCmd>(connection, mediator);
    }
}