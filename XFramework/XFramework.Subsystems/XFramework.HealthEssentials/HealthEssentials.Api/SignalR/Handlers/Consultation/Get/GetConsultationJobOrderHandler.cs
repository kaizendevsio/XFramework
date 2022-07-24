using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Get;

public class GetConsultationJobOrderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetConsultationJobOrderRequest, GetConsultationJobOrderQuery, ConsultationJobOrderResponse>(connection, mediator);
    }
}