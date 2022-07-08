using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Ailment;

public class GetAilmentEntityQuery : GetAilmentEntityRequest, IRequest<QueryResponse<AilmentEntityResponse>>
{
    
}