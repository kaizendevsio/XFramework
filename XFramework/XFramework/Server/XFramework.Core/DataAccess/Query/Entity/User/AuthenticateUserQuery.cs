using MediatR;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Core.DataAccess.Query.Entity.User
{
   public class AuthenticateUserQuery : IRequest<bool>
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}
