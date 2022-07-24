using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceEntityGroupListQuery, QueryResponse<List<PharmacyServiceEntityGroupResponse>>>
{
    public GetPharmacyServiceEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceEntityGroupResponse>>> Handle(GetPharmacyServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}