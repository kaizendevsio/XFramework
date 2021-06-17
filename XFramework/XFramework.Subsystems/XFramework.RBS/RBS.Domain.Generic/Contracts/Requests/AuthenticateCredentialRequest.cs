using XFramework.Domain.Generic.Enums;

namespace RBS.Domain.Generic.Contracts.Requests
{
    public class AuthenticateCredentialRequest : RequestBase
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Remember { get; set; }
        public AuthorizeBy AuthorizeBy { get; set; }
    }
}