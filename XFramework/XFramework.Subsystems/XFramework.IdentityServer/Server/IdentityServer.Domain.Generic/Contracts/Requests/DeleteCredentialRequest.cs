using System;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class DeleteCredentialRequest : RequestBase
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
        
    }
}