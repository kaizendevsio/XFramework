using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyTagHandler : QueryBaseHandler, IRequestHandler<GetPharmacyTagQuery, QueryResponse<PharmacyTagResponse>>
{
    public GetPharmacyTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyTagResponse>> Handle(GetPharmacyTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}