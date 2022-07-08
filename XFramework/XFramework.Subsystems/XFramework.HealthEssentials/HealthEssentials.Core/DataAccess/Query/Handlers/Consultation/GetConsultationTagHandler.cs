using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationTagHandler : QueryBaseHandler, IRequestHandler<GetConsultationTagQuery, QueryResponse<ConsultationTagResponse>>
{
    public GetConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationTagResponse>> Handle(GetConsultationTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}