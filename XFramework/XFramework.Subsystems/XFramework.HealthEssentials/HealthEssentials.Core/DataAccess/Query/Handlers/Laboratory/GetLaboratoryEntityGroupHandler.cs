using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityGroupQuery, QueryResponse<LaboratoryEntityGroupResponse>>
{
    public GetLaboratoryEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryEntityGroupResponse>> Handle(GetLaboratoryEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}