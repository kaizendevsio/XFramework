using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Verifications;

public class GetVerificationHandler : QueryBaseHandler, IRequestHandler<GetVerificationQuery, QueryResponse<IdentityVerificationResponse>>
{
    public GetVerificationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<IdentityVerificationResponse>> Handle(GetVerificationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}