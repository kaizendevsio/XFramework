using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Delete;

public class DeleteTagEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteTagEntityGroupRequest, DeleteTagEntityGroupCmd>(connection, mediator);
    }
}