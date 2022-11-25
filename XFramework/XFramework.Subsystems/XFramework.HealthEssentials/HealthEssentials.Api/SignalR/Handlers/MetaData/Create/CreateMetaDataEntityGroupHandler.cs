using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Create;

public class CreateMetaDataEntityGroupHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateMetaDataEntityGroupRequest, CreateMetaDataEntityGroupCmd>(connection, mediator);
    }
}