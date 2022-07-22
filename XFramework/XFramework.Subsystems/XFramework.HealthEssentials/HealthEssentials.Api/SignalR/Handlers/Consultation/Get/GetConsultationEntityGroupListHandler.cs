using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Get;

public class GetConsultationEntityGroupListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetConsultationEntityGroupListRequest, GetConsultationEntityGroupListQuery, List<ConsultationEntityGroupResponse>>(connection, mediator);
    }
}