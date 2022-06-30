using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class GetContactListQuery : GetContactListRequest, IRequest<QueryResponse<List<ContactResponse>>>
{
    
}