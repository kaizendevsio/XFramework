using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Get;

public class GetDoctorConsultationJobOrderListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetDoctorConsultationJobOrderListRequest, GetDoctorConsultationJobOrderListQuery, List<DoctorConsultationJobOrderResponse>>(connection, mediator);
    }
}