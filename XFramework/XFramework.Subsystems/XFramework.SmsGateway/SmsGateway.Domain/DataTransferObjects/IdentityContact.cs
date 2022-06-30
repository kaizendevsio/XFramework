using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects
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
        public long? UcentitiesId { get; set; }
        public string Value { get; set; } = null!;
        public long? UserCredentialId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual IdentityContactEntity? Ucentities { get; set; }
        public virtual IdentityCredential? UserCredential { get; set; }
    }
}
