using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationTagListQuery, QueryResponse<List<LaboratoryLocationTagResponse>>>
{
    public GetLaboratoryLocationTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryLocationTagResponse>>> Handle(GetLaboratoryLocationTagListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}