using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Doctor;

public class VerifyDoctorIdentityQuery : VerifyDoctorIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}