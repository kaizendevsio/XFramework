namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class AuthorizeIdentityContract
    {
        public string AccessToken  { get; set; }
        public string RefreshToken  { get; set; }
    }
}