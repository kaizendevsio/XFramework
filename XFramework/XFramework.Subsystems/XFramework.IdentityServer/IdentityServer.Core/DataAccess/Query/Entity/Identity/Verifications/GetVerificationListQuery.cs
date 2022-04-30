using IdentityServer.Domain.Generic.Contracts.Requests.Get.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;

public class GetVerificationListQuery : GetVerificationListRequest, IRequest<QueryResponse<List<IdentityVerificationResponse>>>
{
    
}