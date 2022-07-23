using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryTagListQuery, QueryResponse<List<LaboratoryTagResponse>>>
{
    public GetLaboratoryTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryTagResponse>>> Handle(GetLaboratoryTagListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}