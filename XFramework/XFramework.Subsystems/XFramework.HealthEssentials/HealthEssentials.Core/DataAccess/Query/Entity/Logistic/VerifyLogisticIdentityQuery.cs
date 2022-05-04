using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class VerifyLogisticIdentityQuery : VerifyLogisticIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}