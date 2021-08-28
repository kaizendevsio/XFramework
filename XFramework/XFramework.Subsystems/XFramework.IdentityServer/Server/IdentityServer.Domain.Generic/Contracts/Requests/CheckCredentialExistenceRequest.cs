using System;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class CheckCredentialExistenceRequest : RequestBase
    {
        public string UserName { get; set; }
        public Guid Cuid { get; set; }
    }
}