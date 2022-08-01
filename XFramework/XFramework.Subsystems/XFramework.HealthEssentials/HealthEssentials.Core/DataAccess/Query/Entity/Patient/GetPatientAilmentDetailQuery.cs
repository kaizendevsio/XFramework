using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class GetPatientAilmentDetailQuery : GetPatientAilmentDetailRequest, IRequest<QueryResponse<PatientAilmentDetailResponse>>
{
    
}