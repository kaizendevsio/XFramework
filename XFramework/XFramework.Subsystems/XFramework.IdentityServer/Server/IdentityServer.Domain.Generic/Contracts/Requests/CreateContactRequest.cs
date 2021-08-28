using System;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class CreateContactRequest : RequestBase
    {
        public GenericContactType ContactType { get; set; }
        public string Value { get; set; }
        public Guid Cuid { get; set; }
    }
}