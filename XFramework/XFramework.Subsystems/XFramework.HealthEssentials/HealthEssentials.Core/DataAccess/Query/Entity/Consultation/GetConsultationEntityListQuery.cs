using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

public class GetConsultationEntityListQuery : GetConsultationEntityListRequest, IRequest<QueryResponse<List<ConsultationEntityResponse>>>
{
    
}