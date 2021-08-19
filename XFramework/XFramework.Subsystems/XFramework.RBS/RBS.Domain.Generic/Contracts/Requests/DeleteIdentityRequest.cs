using System;

namespace RBS.Domain.Generic.Contracts.Requests
{
    public class DeleteIdentityRequest : RequestBase
    {
        public Guid Uid { get; set; }
    }
}
