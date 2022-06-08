using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class VerifyPatientIdentityQuery : VerifyPatientRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}