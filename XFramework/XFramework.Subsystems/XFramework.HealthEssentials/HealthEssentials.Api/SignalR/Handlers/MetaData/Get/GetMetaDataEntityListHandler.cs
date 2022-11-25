using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Get;

public class GetMetaDataEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMetaDataEntityListRequest, GetMetaDataEntityListQuery, List<MetaDataEntityResponse>>(connection, mediator);
    }
}