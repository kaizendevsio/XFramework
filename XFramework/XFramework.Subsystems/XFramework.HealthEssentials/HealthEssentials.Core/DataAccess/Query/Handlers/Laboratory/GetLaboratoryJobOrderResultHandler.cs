using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultQuery, QueryResponse<LaboratoryJobOrderResultResponse>>
{
    public GetLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultResponse>> Handle(GetLaboratoryJobOrderResultQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}