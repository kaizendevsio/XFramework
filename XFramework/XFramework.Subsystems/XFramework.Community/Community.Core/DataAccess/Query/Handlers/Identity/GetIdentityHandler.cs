using Community.Core.DataAccess.Query.Entity.Identity;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Core.DataAccess.Query.Handlers.Identity;

public class GetIdentityHandler : QueryBaseHandler, IRequestHandler<GetIdentityQuery, QueryResponse<CommunityIdentityResponse>>
{
    public GetIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<CommunityIdentityResponse>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
    {
        CommunityIdentity? communityIdentity = null;

        if (request.CommunityIdentityGuid is not null)
        {
            communityIdentity = await _dataLayer.CommunityIdentities
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.Guid == $"{request.CommunityIdentityGuid}", CancellationToken.None);
        }
        if (request.CredentialGuid is not null)
        {
            communityIdentity = await _dataLayer.CommunityIdentities
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.IdentityCredential.Guid == $"{request.CredentialGuid}",
                    CancellationToken.None);
        }

        if (communityIdentity is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = true,
                Message = "Community identity not found"
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Response = communityIdentity.Adapt<CommunityIdentityResponse>()
        };
    }
}