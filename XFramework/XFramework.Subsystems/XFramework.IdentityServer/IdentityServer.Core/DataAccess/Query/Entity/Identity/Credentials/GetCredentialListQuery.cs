using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class GetCredentialListQuery : GetCredentialListRequest, IRequest<QueryResponse<List<CredentialResponse>>>
{

}