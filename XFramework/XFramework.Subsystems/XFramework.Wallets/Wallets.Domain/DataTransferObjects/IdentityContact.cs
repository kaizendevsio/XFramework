using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class IdentityContact
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? EntityId { get; set; }
        public string Value { get; set; }
        public long? UserCredentialId { get; set; }
        public string Guid { get; set; }
        public long GroupId { get; set; }

        public virtual IdentityContactEntity Entity { get; set; }
        public virtual IdentityContactGroup Group { get; set; }
        public virtual IdentityCredential UserCredential { get; set; }
    }
}
