using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Delete;

public class DeleteMetaDataEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMetaDataEntityRequest, DeleteMetaDataEntityCmd>(connection, mediator);
    }
}