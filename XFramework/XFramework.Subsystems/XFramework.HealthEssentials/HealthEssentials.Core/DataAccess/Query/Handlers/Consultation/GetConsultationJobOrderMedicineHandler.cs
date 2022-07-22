using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderMedicineHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderMedicineQuery, QueryResponse<ConsultationJobOrderMedicineResponse>>
{
    public GetConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderMedicineResponse>> Handle(GetConsultationJobOrderMedicineQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}