using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class GetIdentityCredentialListRequest
    {
        public IdentityRole IdentityRole { get; set; }
    }
}