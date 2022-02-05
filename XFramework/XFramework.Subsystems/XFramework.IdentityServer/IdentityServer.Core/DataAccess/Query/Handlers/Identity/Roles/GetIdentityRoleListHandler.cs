using IdentityServer.Core.DataAccess.Query.Entity.Identity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Roles;

public class GetIdentityRoleListHandler : QueryBaseHandler, IRequestHandler<GetIdentityRoleListQuery, QueryResponseBO<List<IdentityRoleResponse>>>
{
    public GetIdentityRoleListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<List<IdentityRoleResponse>>> Handle(GetIdentityRoleListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblIdentityRoles
            .Where(i => i.UserCred.ApplicationId == request.RequestServer.ApplicationId)
            .Take(1000)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken: cancellationToken);
            
        if (!result.Any())
        {
            return new ()
            {
                Message = $"No identity role exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<IdentityRoleResponse>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = r
        };    
    }
}