using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetDoctorEntityGroupQuery, QueryResponse<DoctorEntityGroupResponse>>
{
    public GetDoctorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorEntityGroupResponse>> Handle(GetDoctorEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}