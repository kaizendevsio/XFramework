using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Verifications;

public class GetVerificationListHandler : QueryBaseHandler, IRequestHandler<GetVerificationListQuery, QueryResponse<List<IdentityVerificationResponse>>>
{
    public GetVerificationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<IdentityVerificationResponse>>> Handle(GetVerificationListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}