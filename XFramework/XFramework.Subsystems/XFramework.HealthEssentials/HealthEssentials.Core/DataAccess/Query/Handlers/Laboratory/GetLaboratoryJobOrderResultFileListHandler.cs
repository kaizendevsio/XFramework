using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultFileListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultFileListQuery, QueryResponse<List<LaboratoryJobOrderResultFileResponse>>>
{
    public GetLaboratoryJobOrderResultFileListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultFileResponse>>> Handle(GetLaboratoryJobOrderResultFileListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}