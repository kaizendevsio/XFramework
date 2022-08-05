using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorEntityHandler : QueryBaseHandler, IRequestHandler<GetDoctorEntityQuery, QueryResponse<DoctorEntityResponse>>
{
    public GetDoctorEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorEntityResponse>> Handle(GetDoctorEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}