using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Update;

public class UpdateLaboratoryLocationTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateLaboratoryLocationTagRequest, UpdateLaboratoryLocationTagCmd>(connection, mediator);
    }
}