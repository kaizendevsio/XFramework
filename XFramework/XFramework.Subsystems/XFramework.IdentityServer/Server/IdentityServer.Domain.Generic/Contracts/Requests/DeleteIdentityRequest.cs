using System;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class DeleteIdentityRequest : RequestBase
    {
        public Guid Uid { get; set; }
    }
}
