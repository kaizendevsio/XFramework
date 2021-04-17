using IdentityServer.Domain.Contracts;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential
{
    public class AuthenticateCredentialQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityContract>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}