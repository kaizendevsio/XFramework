using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Doctor;

public class GetDoctorQuery : GetDoctorRequest, IRequest<QueryResponse<DoctorResponse>>
{
    
}