namespace IdentityServer.Domain.Contracts
{
    public class AuthorizeIdentityContract
    {
        public string AccessToken  { get; set; }
        public string RefreshToken  { get; set; }
    }
}