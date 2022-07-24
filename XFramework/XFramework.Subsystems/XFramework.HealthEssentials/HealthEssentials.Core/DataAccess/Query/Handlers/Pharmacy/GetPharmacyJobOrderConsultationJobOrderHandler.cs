using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderConsultationJobOrderQuery, QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>>
{
    public GetPharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>> Handle(GetPharmacyJobOrderConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}