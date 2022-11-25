using HealthEssentials.Core.DataAccess.Query.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.MetaData;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Get;

public class GetMetaDataEntityGroupListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMetaDataEntityGroupListRequest, GetMetaDataEntityGroupListQuery, List<MetaDataEntityGroupResponse>>(connection, mediator);
    }
}