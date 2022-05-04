using HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

public class VerifyPharmacyIdentityQuery : VerifyPharmacyIdentityRequest, IRequest<QueryResponse<IdentityValidationResponse>>
{
}