using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

public class GetConsultationJobOrderQuery : GetConsultationJobOrderRequest, IRequest<QueryResponse<ConsultationJobOrderResponse>>
{
    
}