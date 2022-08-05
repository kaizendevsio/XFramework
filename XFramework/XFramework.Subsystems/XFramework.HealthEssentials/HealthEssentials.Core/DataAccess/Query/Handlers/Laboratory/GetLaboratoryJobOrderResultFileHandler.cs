using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultFileHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultFileQuery, QueryResponse<LaboratoryJobOrderResultFileResponse>>
{
    public GetLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultFileResponse>> Handle(GetLaboratoryJobOrderResultFileQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}