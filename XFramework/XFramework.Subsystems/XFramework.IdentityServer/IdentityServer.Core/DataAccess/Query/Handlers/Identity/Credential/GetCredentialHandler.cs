using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class GetCredentialHandler : QueryBaseHandler ,IRequestHandler<GetCredentialQuery, QueryResponseBO<IdentityCredentialResponse>>
{
    public GetCredentialHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponseBO<IdentityCredentialResponse>> Handle(GetCredentialQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityCredentials
            .Include( i => i.IdentityInfo)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<IdentityCredentialResponse>()
        };
    }
}