using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Delete;

public class DeleteTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteTagRequest, DeleteTagCmd>(connection, mediator);
    }
}