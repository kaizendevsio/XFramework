using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class GetContactQuery : QueryBaseEntity, IRequest<QueryResponse<ContactResponse>>
{
    public Guid? Guid { get; set; }   
}