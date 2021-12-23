using System;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class DeleteCredentialRequest : RequestBase
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
        
    }
}