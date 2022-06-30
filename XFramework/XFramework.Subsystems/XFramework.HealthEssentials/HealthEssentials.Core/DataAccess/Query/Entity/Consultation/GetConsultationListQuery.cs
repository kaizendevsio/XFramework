namespace HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

public class GetConsultationListQuery : GetConsultationListRequest, IRequest<QueryResponse<List<ConsultationResponse>>>
{
    
}