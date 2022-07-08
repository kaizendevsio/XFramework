using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetConsultationEntityGroupQuery, QueryResponse<ConsultationEntityGroupResponse>>
{
    public GetConsultationEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationEntityGroupResponse>> Handle(GetConsultationEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}