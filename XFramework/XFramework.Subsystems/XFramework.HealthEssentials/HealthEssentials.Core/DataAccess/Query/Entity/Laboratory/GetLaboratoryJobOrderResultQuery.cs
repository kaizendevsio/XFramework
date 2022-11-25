using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryJobOrderResultQuery : GetLaboratoryJobOrderResultRequest, IRequest<QueryResponse<LaboratoryJobOrderResultResponse>>
{
    
}