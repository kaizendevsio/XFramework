using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceTagListQuery, QueryResponse<List<LaboratoryServiceTagResponse>>>
{
    public GetLaboratoryServiceTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryServiceTagResponse>>> Handle(GetLaboratoryServiceTagListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}