using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Roles;

public class GetRoleEntityListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<RoleEntityResponse>>>
{
        
}