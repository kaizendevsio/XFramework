using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationHandler : QueryBaseHandler, IRequestHandler<GetConsultationQuery, QueryResponse<ConsultationResponse>>
{
    public GetConsultationHandler(DataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<ConsultationResponse>> Handle(GetConsultationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}