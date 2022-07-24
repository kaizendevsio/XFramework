using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceQuery, QueryResponse<PharmacyServiceResponse>>
{
    public GetPharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceResponse>> Handle(GetPharmacyServiceQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}