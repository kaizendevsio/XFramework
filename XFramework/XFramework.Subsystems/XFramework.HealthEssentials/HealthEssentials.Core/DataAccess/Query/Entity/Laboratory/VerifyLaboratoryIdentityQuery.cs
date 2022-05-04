using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class VerifyLaboratoryIdentityQuery : VerifyLaboratoryIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}