using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Roles;

public class GetIdentityRoleListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<IdentityCredentialResponse>>>
{
}