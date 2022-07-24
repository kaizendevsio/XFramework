using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderMedicineHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderMedicineQuery, QueryResponse<PharmacyJobOrderMedicineResponse>>
{
    public GetPharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderMedicineResponse>> Handle(GetPharmacyJobOrderMedicineQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}