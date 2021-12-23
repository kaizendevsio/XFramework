using System;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class GetIdentityRequest : RequestBase
    {
        public Guid Uid { get; set; }
    }
}