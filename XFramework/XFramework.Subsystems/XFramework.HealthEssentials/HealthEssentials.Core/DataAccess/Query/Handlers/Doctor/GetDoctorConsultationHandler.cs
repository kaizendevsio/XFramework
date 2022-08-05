using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationQuery, QueryResponse<DoctorConsultationResponse>>
{
    public GetDoctorConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorConsultationResponse>> Handle(GetDoctorConsultationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}