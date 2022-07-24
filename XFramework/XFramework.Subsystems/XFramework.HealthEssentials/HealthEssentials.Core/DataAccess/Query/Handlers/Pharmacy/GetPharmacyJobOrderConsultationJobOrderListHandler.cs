using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderConsultationJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderConsultationJobOrderListQuery, QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>>
{
    public GetPharmacyJobOrderConsultationJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>> Handle(GetPharmacyJobOrderConsultationJobOrderListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}