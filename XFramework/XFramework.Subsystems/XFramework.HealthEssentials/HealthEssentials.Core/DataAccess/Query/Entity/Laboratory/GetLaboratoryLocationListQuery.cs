using HealthEssentials.Core.DataAccess.Query.Handlers;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryLocationListQuery : GetLaboratoryLocationListRequest, IRequest<QueryResponse<List<LaboratoryLocationResponse>>>
{
    
}