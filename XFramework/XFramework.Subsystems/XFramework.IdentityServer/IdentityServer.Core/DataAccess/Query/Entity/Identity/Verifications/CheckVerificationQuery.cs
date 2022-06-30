using IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;

public class CheckVerificationQuery : CheckVerificationRequest, IRequest<QueryResponse<IdentityVerificationSummaryResponse>>
{
    
}