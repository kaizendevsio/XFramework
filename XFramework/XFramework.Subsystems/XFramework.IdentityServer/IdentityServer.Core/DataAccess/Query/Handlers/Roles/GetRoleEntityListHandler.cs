using IdentityServer.Core.DataAccess.Query.Entity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Roles;

public class GetRoleEntityListHandler : QueryBaseHandler, IRequestHandler<GetRoleEntityListQuery, QueryResponseBO<List<RoleEntityResponse>>>
{
    public GetRoleEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<List<RoleEntityResponse>>> Handle(GetRoleEntityListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblIdentityRoleEntities
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

        var r = result.Adapt<List<RoleEntityResponse>>();
        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = r
        };    
    }
}