using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityQuery, QueryResponse<LaboratoryEntityResponse>>
{
    public GetLaboratoryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryEntityResponse>> Handle(GetLaboratoryEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}