using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Get;

public class GetHospitalLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetHospitalLaboratoryRequest, GetHospitalLaboratoryQuery, HospitalLaboratoryResponse>(connection, mediator);
    }
}