using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Patient;

public class VerifyPatientIdentityQuery : VerifyPatientIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}