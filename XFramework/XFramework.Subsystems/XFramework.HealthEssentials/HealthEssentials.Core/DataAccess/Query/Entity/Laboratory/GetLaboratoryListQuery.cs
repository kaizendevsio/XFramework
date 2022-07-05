using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryListQuery : GetLaboratoryListRequest, IRequest<QueryResponse<List<LaboratoryResponse>>>
{
    
}