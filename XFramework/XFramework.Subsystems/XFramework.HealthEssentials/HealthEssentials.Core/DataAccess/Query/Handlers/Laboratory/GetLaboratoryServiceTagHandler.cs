using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceTagHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceTagQuery, QueryResponse<LaboratoryServiceTagResponse>>
{
    public GetLaboratoryServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryServiceTagResponse>> Handle(GetLaboratoryServiceTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}