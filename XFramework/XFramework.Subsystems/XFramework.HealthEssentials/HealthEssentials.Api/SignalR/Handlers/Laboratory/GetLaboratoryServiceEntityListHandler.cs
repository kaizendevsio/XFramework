using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory;

public class GetLaboratoryServiceEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetLaboratoryServiceEntityListRequest, GetLaboratoryServiceEntityListQuery, List<LaboratoryServiceEntityResponse>>(connection, mediator);
    }
}