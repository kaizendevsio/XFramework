using System;

namespace RBS.Domain.Generic.Contracts.Requests
{
    public class GetIdentityRequest : RequestBase
    {
        public Guid Uid { get; set; }
    }
}