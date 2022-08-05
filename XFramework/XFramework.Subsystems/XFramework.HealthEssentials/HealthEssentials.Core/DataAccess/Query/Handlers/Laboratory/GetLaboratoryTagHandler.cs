using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryTagHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryTagQuery, QueryResponse<LaboratoryTagResponse>>
{
    public GetLaboratoryTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryTagResponse>> Handle(GetLaboratoryTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}