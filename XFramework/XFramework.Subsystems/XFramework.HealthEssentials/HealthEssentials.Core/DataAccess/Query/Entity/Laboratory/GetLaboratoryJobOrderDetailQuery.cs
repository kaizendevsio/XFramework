using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryJobOrderDetailQuery : GetLaboratoryJobOrderDetailRequest, IRequest<QueryResponse<LaboratoryJobOrderDetailResponse>>
{
    
}