using System;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class CheckCredentialExistenceRequest : RequestBase
    {
        public string UserName { get; set; }
        public string Cuid { get; set; }
    }
}