using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Create;

public class CreateLaboratoryJobOrderResultHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLaboratoryJobOrderResultRequest, CreateLaboratoryJobOrderResultCmd>(connection, mediator);
    }
}