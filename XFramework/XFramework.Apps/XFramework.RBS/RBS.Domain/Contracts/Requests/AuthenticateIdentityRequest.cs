using RBS.Domain.Enums;

namespace RBS.Domain.Contracts.Requests
{
    public class AuthenticateIdentityRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}