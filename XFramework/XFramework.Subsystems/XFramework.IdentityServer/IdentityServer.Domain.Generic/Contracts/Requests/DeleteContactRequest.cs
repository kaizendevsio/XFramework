using System;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class DeleteContactRequest
    {
        public GenericContactType ContactType { get; set; }
        public Guid Cuid { get; set; }
        public long Cid { get; set; }
    }
}