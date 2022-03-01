using IdentityServer.Core.DataAccess.Query.Entity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Roles;

public class GetRoleEntityListHandler : QueryBaseHandler, IRequestHandler<GetRoleEntityListQuery, QueryResponse<List<RoleEntityResponse>>>
{
    public GetRoleEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<List<RoleEntityResponse>>> Handle(GetRoleEntityListQuery request, CancellationToken cancellationToken)
    {
        var appEntity = await _dataLayer.TblApplications.FirstOrDefaultAsync(i => i.Guid == $"{request.ApplicationGuid}", cancellationToken);
        if (appEntity == null)
        {
            return new ()
            {
                Message = $"Identity with Guid {request.ApplicationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var result = await _dataLayer.TblIdentityRoleEntities
            .Where(i => i.ApplicationId == appEntity.Id)
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