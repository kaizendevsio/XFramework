using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationTagHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationTagQuery, QueryResponse<LaboratoryLocationTagResponse>>
{
    public GetLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryLocationTagResponse>> Handle(GetLaboratoryLocationTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}