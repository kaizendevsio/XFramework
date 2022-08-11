using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalTagHandler : QueryBaseHandler, IRequestHandler<GetHospitalTagQuery, QueryResponse<HospitalTagResponse>>
{
    public GetHospitalTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalTagResponse>> Handle(GetHospitalTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}