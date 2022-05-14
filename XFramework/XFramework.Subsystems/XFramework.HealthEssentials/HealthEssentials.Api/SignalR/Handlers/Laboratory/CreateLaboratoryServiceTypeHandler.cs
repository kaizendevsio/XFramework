using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class CreateLaboratoryServiceTypeHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLaboratoryServiceTypeRequest, CreateLaboratoryServiceTypeCmd>(connection, mediator);
    }
}