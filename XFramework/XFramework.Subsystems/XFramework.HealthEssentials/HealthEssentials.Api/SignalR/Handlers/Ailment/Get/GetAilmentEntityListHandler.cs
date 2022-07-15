using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Api.SignalR.Handlers.Ailment.Get;

public class GetAilmentEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetAilmentEntityListRequest, GetAilmentEntityListQuery, List<AilmentEntityResponse>>(connection, mediator);
    }
}