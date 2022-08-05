using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

public class GetConsultationJobOrderListQuery : GetConsultationJobOrderListRequest, IRequest<QueryResponse<List<ConsultationJobOrderResponse>>>
{
    
}