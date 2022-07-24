using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceTagHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceTagQuery, QueryResponse<PharmacyServiceTagResponse>>
{
    public GetPharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceTagResponse>> Handle(GetPharmacyServiceTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}