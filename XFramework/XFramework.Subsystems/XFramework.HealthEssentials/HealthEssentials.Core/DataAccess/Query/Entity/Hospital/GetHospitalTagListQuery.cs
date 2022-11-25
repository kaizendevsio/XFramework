using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Hospital;

public class GetHospitalTagListQuery : GetHospitalTagListRequest, IRequest<QueryResponse<List<HospitalTagResponse>>>
{
    
}