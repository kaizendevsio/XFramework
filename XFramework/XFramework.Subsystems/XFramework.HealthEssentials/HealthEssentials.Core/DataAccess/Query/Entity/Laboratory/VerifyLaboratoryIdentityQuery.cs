using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class VerifyLaboratoryIdentityQuery : VerifyLaboratoryMemberRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}