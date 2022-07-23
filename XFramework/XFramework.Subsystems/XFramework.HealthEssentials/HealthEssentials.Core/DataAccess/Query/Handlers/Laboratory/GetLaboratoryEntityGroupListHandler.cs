using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityGroupListQuery, QueryResponse<List<LaboratoryEntityGroupResponse>>>
{
    public GetLaboratoryEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryEntityGroupResponse>>> Handle(GetLaboratoryEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}