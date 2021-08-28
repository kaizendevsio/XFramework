using System;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class CheckIdentityExistenceRequest : RequestBase
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Guid Uid { get; set; }
    }
}