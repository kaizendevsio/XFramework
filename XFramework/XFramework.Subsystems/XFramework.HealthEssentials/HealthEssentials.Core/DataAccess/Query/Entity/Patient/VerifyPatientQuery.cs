using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Verify;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class VerifyPatientQuery : VerifyPatientRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}