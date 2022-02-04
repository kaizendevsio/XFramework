using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Core.DataAccess.Query.Entity.Identity
{
   public class AuthenticateIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityResponse>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}
