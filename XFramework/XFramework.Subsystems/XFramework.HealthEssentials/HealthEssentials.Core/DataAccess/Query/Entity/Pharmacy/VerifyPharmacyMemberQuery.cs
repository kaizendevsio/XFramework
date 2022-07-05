using HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Verify;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class VerifyPharmacyMemberQuery : VerifyPharmacyMemberRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}