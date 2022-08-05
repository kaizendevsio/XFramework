using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Update;

public class UpdateLaboratoryServiceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateLaboratoryServiceRequest, UpdateLaboratoryServiceCmd>(connection, mediator);
    }
}