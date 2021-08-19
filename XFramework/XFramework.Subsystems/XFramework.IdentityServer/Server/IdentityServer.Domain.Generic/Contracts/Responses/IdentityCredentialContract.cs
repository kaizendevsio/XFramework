using System;

namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class IdentityCredentialContract
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
        public string UserName { get; set; }
        public string UserAlias { get; set; }
        public short? LogInStatus { get; set; }
        public long ApplicationId { get; set; }
        public string Token { get; set; }
        public string Cuid { get; set; }

        public virtual IdentityInfoContract IdentityInfo { get; set; }
    }
}