using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultListQuery, QueryResponse<List<LaboratoryJobOrderResultResponse>>>
{
    public GetLaboratoryJobOrderResultListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultResponse>>> Handle(GetLaboratoryJobOrderResultListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}