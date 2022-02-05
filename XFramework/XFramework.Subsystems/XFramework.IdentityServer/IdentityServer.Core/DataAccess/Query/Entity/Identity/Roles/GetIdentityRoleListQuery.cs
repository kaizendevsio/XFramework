using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Roles;

public class GetIdentityRoleListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<IdentityRoleResponse>>>
{
}