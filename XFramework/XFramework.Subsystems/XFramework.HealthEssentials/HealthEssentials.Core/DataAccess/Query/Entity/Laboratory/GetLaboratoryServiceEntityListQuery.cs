using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryServiceEntityListQuery : GetLaboratoryServiceEntityListRequest, IRequest<QueryResponse<List<LaboratoryServiceEntityResponse>>>
{
    
}