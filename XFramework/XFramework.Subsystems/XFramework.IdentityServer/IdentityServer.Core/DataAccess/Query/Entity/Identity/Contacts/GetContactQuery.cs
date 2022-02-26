using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class GetContactQuery : GetContactRequest, IRequest<QueryResponse<ContactResponse>>
{
    
}