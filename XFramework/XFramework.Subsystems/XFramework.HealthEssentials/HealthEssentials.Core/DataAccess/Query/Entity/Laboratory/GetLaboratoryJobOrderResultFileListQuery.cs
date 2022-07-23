using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryJobOrderResultFileListQuery : GetLaboratoryJobOrderResultFileListRequest, IRequest<QueryResponse<List<LaboratoryJobOrderResultFileResponse>>>
{
    
}