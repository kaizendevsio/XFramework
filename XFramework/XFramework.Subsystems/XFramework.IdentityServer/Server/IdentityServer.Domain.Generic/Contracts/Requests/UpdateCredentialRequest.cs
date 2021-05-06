using System;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class UpdateCredentialRequest : RequestBase
    {
        public Guid Uid { get; set; }
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
    }
}