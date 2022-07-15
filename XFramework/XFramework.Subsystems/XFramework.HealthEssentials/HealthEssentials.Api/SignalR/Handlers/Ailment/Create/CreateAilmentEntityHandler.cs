using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Ailment.Create;

public class CreateAilmentEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateAilmentEntityRequest, CreateAilmentEntityCmd>(connection, mediator);
    }
}