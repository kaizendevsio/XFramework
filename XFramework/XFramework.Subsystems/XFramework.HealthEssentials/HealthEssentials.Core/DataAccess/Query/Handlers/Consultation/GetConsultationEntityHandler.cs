using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationEntityHandler : QueryBaseHandler, IRequestHandler<GetConsultationEntityQuery, QueryResponse<ConsultationEntityResponse>>
{
    public GetConsultationEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationEntityResponse>> Handle(GetConsultationEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}