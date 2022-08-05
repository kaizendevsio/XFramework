using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorTagHandler : QueryBaseHandler, IRequestHandler<GetDoctorTagQuery, QueryResponse<DoctorTagResponse>>
{
    public GetDoctorTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorTagResponse>> Handle(GetDoctorTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}