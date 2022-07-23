using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityListQuery, QueryResponse<List<LaboratoryEntityResponse>>>
{
    public GetLaboratoryEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryEntityResponse>>> Handle(GetLaboratoryEntityListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}