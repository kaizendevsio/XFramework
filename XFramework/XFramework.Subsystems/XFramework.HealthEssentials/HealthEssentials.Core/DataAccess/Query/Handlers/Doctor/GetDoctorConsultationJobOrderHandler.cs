using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationJobOrderQuery, QueryResponse<DoctorConsultationJobOrderResponse>>
{
    public GetDoctorConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorConsultationJobOrderResponse>> Handle(GetDoctorConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}