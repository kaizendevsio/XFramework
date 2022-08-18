using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Get;

public class GetHospitalTagListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetHospitalTagListRequest, GetHospitalTagListQuery, List<HospitalTagResponse>>(connection, mediator);
    }
}