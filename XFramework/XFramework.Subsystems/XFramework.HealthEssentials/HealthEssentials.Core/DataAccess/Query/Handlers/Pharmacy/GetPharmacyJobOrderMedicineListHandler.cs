using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderMedicineListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderMedicineListQuery, QueryResponse<List<PharmacyJobOrderMedicineResponse>>>
{
    public GetPharmacyJobOrderMedicineListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderMedicineResponse>>> Handle(GetPharmacyJobOrderMedicineListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}