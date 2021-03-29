using MediatR;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Contracts;
using StreamFlow.Domain.Enums;

namespace StreamFlow.Core.DataAccess.Query.Entity.Identity.Authorization
{
    public class AuthenticateIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityContract>>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}