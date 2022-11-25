using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Update;

public class UpdateMetaDataEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateMetaDataEntityRequest, UpdateMetaDataEntityCmd>(connection, mediator);
    }
}