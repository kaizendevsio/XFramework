using IdentityServer.Domain.Generic.Contracts.Requests.Get.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;

public class GetVerificationQuery : GetVerificationRequest, IRequest<QueryResponse<IdentityVerificationResponse>>
{
    
}