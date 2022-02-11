using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class GetCredentialListQuery : GetCredentialListRequest, IRequest<QueryResponseBO<List<CredentialResponse>>>
{
    public RoleEntity IdentityRole { get; set; }
    public string SearchString { get; set; }
    public int ListCount { get; set; } = 50;
    public bool Filter { get; set; }
    public int Skip { get; set; }
}