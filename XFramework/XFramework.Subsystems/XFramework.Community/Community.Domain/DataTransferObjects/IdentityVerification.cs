using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class IdentityVerification
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public long? IdentityCredId { get; set; }
        public long? VerificationTypeId { get; set; }
        public short? Status { get; set; }
        public DateTimeOffset? StatusUpdatedOn { get; set; }
        public string? Token { get; set; }
        public DateTime? Expiry { get; set; }
        public string Guid { get; set; } = null!;

        public virtual IdentityCredential? IdentityCred { get; set; }
        public virtual IdentityVerificationEntity? VerificationType { get; set; }
    }
}
