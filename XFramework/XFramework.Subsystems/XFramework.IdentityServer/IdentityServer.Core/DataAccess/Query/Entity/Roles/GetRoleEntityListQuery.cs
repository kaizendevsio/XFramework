using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Roles;

public class GetRoleEntityListQuery : GetRoleEntityListRequest, IRequest<QueryResponse<List<RoleEntityResponse>>>
{
        
}