using HealthEssentials.Core.DataAccess.Query.Entity.Tag;
using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Api.SignalR.Handlers.Tag.Get;

public class GetTagEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetTagEntityRequest, GetTagEntityQuery, TagEntityResponse>(connection, mediator);
    }
}