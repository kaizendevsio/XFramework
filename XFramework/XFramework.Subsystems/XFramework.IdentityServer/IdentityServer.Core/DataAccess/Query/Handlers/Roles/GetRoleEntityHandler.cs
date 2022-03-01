using IdentityServer.Core.DataAccess.Query.Entity.Roles;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Roles;

public class GetRoleEntityHandler : QueryBaseHandler, IRequestHandler<GetRoleEntityQuery, QueryResponse<RoleEntityResponse>>
{
    public GetRoleEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<RoleEntityResponse>> Handle(GetRoleEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityRoleEntities
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Identity role with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<RoleEntityResponse>()
        };
    }
}