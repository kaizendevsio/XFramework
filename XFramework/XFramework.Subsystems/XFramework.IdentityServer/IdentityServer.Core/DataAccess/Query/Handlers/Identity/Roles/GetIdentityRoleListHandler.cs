using IdentityServer.Core.DataAccess.Query.Entity.Identity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Roles;

public class GetIdentityRoleListHandler : QueryBaseHandler, IRequestHandler<GetRoleListQuery, QueryResponse<List<RoleResponse>>>
{
    public GetIdentityRoleListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<List<RoleResponse>>> Handle(GetRoleListQuery request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);
        
        var result = await _dataLayer.IdentityRoles
            .Where(i => i.UserCred.ApplicationId == application.Id)
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

        var r = result.Adapt<List<RoleResponse>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = r
        };    
    }
}