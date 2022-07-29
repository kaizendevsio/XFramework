using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceEntityHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceEntityQuery, QueryResponse<HospitalServiceEntityResponse>>
{
    public GetHospitalServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalServiceEntityResponse>> Handle(GetHospitalServiceEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}