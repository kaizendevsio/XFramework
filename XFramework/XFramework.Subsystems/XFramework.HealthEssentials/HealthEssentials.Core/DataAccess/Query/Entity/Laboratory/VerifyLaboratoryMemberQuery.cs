using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Verify;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class VerifyLaboratoryMemberQuery : VerifyLaboratoryMemberRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}