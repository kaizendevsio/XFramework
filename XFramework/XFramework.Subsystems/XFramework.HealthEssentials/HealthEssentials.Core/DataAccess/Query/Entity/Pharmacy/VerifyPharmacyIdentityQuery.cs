using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class VerifyPharmacyIdentityQuery : VerifyPharmacyMemberRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}