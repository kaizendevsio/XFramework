using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity;

public class GetIdentityQuery : GetIdentityRequest, IRequest<QueryResponse<IdentityResponse>>
{
     
}