using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderQuery, QueryResponse<ConsultationJobOrderResponse>>
{
    public GetConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderResponse>> Handle(GetConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}