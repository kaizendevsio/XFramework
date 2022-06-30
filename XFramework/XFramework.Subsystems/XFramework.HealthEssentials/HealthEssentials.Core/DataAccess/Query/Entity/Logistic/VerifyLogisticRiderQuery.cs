using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class VerifyLogisticRiderQuery : VerifyLogisticRiderRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}