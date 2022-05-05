using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class VerifyLogisticIdentityQuery : VerifyLogisticIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}