using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceEntityGroupQuery, QueryResponse<PharmacyServiceEntityGroupResponse>>
{
    public GetPharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceEntityGroupResponse>> Handle(GetPharmacyServiceEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}