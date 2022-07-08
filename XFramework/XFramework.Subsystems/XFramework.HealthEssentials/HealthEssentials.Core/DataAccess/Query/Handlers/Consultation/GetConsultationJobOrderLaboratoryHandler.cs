using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderLaboratoryQuery, QueryResponse<ConsultationJobOrderLaboratoryResponse>>
{
    public GetConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderLaboratoryResponse>> Handle(GetConsultationJobOrderLaboratoryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}