using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

public class GetConsultationJobOrderLaboratoryQuery : GetConsultationJobOrderLaboratoryRequest, IRequest<QueryResponse<ConsultationJobOrderLaboratoryResponse>>
{
    
}