using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class GetCredentialByContactHandler : QueryBaseHandler ,IRequestHandler<GetCredentialByContactQuery, QueryResponse<CredentialResponse>>
{
    public GetCredentialByContactHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<CredentialResponse>> Handle(GetCredentialByContactQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityCredentials
            .Include( i => i.IdentityInfo)
            .Include( i => i.IdentityRoles)
            .Include( i => i.IdentityContacts)
            .ThenInclude( i => i.Ucentities)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.IdentityContacts.Any(o => o.Value == request.ContactValue), cancellationToken);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Credential with contact '{request.ContactValue}' does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<CredentialResponse>()
        };
    }
}