using IdentityServer.Core.DataAccess.Query.Entity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Roles;

public class GetIdentityRoleListHandler : QueryBaseHandler, IRequestHandler<GetIdentityRoleListQuery, QueryResponseBO<List<IdentityCredentialResponse>>>
{
    public GetIdentityRoleListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<List<IdentityCredentialResponse>>> Handle(GetIdentityRoleListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblIdentityCredentials
            .Where(i => i.ApplicationId == request.RequestServer.ApplicationId)
            .Include(i => i.IdentityInfo)
            .Include(i => i.TblIdentityRoles)
            .Take(1000)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
            
        if (!result.Any())
        {
            return new ()
            {
                Message = $"No identity role exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<IdentityCredentialResponse>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = r
        };    
    }
}