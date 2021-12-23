using MediatR;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;
using RBS.Domain.Enums;

namespace RBS.Core.DataAccess.Query.Entity.Identity.Authorization
{
    public class AuthenticateIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityContract>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}