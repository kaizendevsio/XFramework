using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class Subscription
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long EntityId { get; set; }
        public long? CredentialId { get; set; }
        public string Value { get; set; }
        public short? Status { get; set; }
        public DateTime? ExpireAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Guid { get; set; }

        public virtual IdentityCredential Credential { get; set; }
        public virtual SubscriptionEntity Entity { get; set; }
    }
}
