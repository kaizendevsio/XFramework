using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class UpdateLaboratoryMemberHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateLaboratoryMemberRequest, UpdateLaboratoryMemberCmd>(connection, mediator);
    }
}