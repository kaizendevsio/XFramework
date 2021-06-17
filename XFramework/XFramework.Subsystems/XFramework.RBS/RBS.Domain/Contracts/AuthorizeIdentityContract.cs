
namespace RBS.Domain.Contracts
{
    public class AuthorizeIdentityContract
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long UserAuthId { get; set; }
        public string UserName { get; set; }
    }
}