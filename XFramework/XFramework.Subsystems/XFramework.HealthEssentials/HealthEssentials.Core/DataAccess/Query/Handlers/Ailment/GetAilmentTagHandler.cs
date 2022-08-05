using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentTagHandler : QueryBaseHandler, IRequestHandler<GetAilmentTagQuery, QueryResponse<AilmentTagResponse>>
{
    public GetAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<AilmentTagResponse>> Handle(GetAilmentTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}