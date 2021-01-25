using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.Enums;
using MediatR;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization
{
    public class AuthorizeIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityContract>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}