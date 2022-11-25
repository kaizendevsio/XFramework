using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

namespace HealthEssentials.Api.SignalR.Handlers.Laboratory.Get;

public class GetLaboratoryServiceEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetLaboratoryServiceEntityRequest, GetLaboratoryServiceEntityQuery, LaboratoryServiceEntityResponse>(connection, mediator);
    }
}