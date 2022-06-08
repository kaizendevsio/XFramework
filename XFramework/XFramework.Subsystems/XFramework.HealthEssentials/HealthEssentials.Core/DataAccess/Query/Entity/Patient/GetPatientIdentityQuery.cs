using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class GetPatientIdentityQuery : GetPatientRequest, IRequest<QueryResponse<PatientResponse>>
{
    
}