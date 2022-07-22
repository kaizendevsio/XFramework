using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Delete;

public class DeleteMetaDataEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMetaDataEntityGroupRequest, DeleteMetaDataEntityGroupCmd>(connection, mediator);
    }
}