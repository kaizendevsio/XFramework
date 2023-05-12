using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class SessionDatum
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? SessionEntityId { get; set; }
        public long? UserCredentialId { get; set; }
        public string SessionData { get; set; }
        public string Guid { get; set; }

        public virtual SessionEntity SessionEntity { get; set; }
        public virtual IdentityCredential UserCredential { get; set; }
    }
}
