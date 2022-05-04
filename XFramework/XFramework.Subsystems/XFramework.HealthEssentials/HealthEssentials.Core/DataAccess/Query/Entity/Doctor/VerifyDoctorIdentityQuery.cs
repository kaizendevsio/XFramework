using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Doctor;

public class VerifyDoctorIdentityQuery : VerifyDoctorIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}